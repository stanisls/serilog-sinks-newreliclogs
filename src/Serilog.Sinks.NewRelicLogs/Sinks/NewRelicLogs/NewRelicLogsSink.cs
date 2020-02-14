using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.NewRelicLogs
{
    internal class NewRelicLogsSink : PeriodicBatchingSink
    {
        public const int DefaultBatchPostingLimit = 1000;
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);

        public string EndpointUrl { get; }
        public string ApplicationName { get; }
        public string LicenseKey { get; }
        public string InsertKey { get; }
        private IFormatProvider FormatProvider { get; }

        public NewRelicLogsSink(string endpointUrl, string applicationName, string licenseKey, string insertKey, int batchSizeLimit, TimeSpan period, IFormatProvider formatProvider = null)
            : base(batchSizeLimit, period)
        {
            EndpointUrl = endpointUrl;
            ApplicationName = applicationName;
            LicenseKey = licenseKey;
            InsertKey = insertKey;
            FormatProvider = formatProvider;
        }

        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            dynamic detailedLogObject = new
            {
                common = new
                {
                    attributes = new Dictionary<string, string>
                    {
                        { "application", ApplicationName }
                    }
                },

                logs = new List<object>()
            };

            var eventList = events.ToList();
            foreach (var logEvent in eventList)
            {
                dynamic logEntry = new
                {
                    timestamp = UnixTimestampFromDateTime(logEvent.Timestamp.UtcDateTime),
                    message = logEvent.RenderMessage(FormatProvider),
                    attributes = new Dictionary<string, string>
                    {
                        { "level", logEvent.Level.ToString() },
                        { "stack_trace", logEvent.Exception?.StackTrace ?? "" },
                    }
                };
                foreach (var prop in logEvent.Properties)
                {
                    if (prop.Key.Equals("newrelic.linkingmetadata", StringComparison.InvariantCultureIgnoreCase))
                    {
                        UnrollNewRelicDistributedTraceAttributes(logEntry, prop.Value);
                    }
                    else
                    {
                        logEntry.attributes.Add(ClearEnclosingQuotes(prop.Key),
                                                ClearEnclosingQuotes(prop.Value.ToString()));
                    }
                }

                detailedLogObject.logs.Add(logEntry);
            }

            var body = Serialize(new List<object> { detailedLogObject }, eventList.Count);

            await Task.Run(() =>
                {
                    try
                    {
                        SendToNewRelicLogs(body);
                    }
                    catch (Exception ex)
                    {
                        SelfLog.WriteLine("Event batch could not be sent to NewRelic Logs and was dropped: {0}", ex);
                    }
                })
                .ConfigureAwait(false);
        }

        private static void UnrollNewRelicDistributedTraceAttributes(dynamic logEntry, LogEventPropertyValue propValue)
        {
            if (!(propValue is DictionaryValue newRelicProperties))
            {
                return;
            }

            foreach (var newRelicProperty in newRelicProperties.Elements)
            {
                logEntry.attributes.Add(ClearEnclosingQuotes(newRelicProperty.Key.ToString()),
                                        ClearEnclosingQuotes(newRelicProperty.Value.ToString()));
            }
        }

        private void SendToNewRelicLogs(string body)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            if (!(WebRequest.Create(EndpointUrl) is HttpWebRequest request))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(LicenseKey))
            {
                request.Headers.Add("X-License-Key", LicenseKey);
            }
            else
            {
                request.Headers.Add("X-Insert-Key", InsertKey);
            }

            request.Headers.Add("Content-Encoding", "gzip");

            request.Timeout = 40000; //It's basically fire-and-forget
            request.Credentials = CredentialCache.DefaultCredentials;
            request.ContentType = "application/gzip";
            request.Accept = "*/*";
            request.Method = "POST";
            request.KeepAlive = false;

            var byteStream = Encoding.UTF8.GetBytes(body);

            try
            {
                using (var zippedRequestStream = new GZipStream(request.GetRequestStream(), CompressionMode.Compress))
                {
                    zippedRequestStream.Write(byteStream, 0, byteStream.Length);
                    zippedRequestStream.Flush();
                    zippedRequestStream.Close();
                }
            }
            catch (WebException e)
            {
                SelfLog.WriteLine("Failed to create WebRequest to NewRelic Logs: {0}", e);
                return;
            }

            try
            {
                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    if (response == null || response.StatusCode != HttpStatusCode.Accepted)
                    {
                        SelfLog.WriteLine("Self-log: Response from NewRelic Logs is missing or negative: {0}", response?.StatusCode);
                    }
                }
            }
            catch (WebException e)
            {
                SelfLog.WriteLine("Failed to parse response from NewRelic Logs: {0}", e);
            }
        }

        private static string Serialize(List<object> blob, int eventCount)
        {
            var serializer = new JsonSerializer();

            //Stipulate 350 bytes per log entry on average
            var json = new StringBuilder(eventCount * 350);

            using (var sw = new StringWriter(json))
            {
                using (var jw = new JsonTextWriter(sw))
                {
                    serializer.Serialize(jw, blob);
                }
            }

            return json.ToString();
        }

        /// <summary>
        /// Converts from DateTime to Unix time. Conversion is timezone-agnostic.
        /// </summary>
        /// <param name="date"></param>
        public long UnixTimestampFromDateTime(DateTime date)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            if (date == DateTime.MinValue) return 0;

            return (long) (date - epoch).TotalMilliseconds;
        }

        private static string ClearEnclosingQuotes(string val)
        {
            if (val.Length > 2 && val.StartsWith(@"""") && val.EndsWith(@"""")
             || val.Length > 4 && val.StartsWith(@"\""") && val.EndsWith(@"\"""))
            {
                var data = new StringBuilder(val);
                data.Replace(@"""", "", 0, 1);
                data.Replace(@"""", "", data.Length - 1, 1);
                data.Replace(@"\""", "", 0, 2);
                data.Replace(@"\""", "", data.Length - 2, 2);

                return data.ToString();
            }

            return val;
        }
    }
}
