using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.NewRelic
{
    internal class NewRelicSink : PeriodicBatchingSink
    {
        public const int DefaultBatchPostingLimit = 1000;
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);

        private IFormatProvider FormatProvider { get; }
        private string CustomEventName { get; }

        public NewRelicSink(string applicationName, int batchSizeLimit, TimeSpan period, string customEventName, IFormatProvider formatProvider = null)
            : base(batchSizeLimit, period)
        {
            CustomEventName = customEventName;
            FormatProvider = formatProvider;

            global::NewRelic.Api.Agent.NewRelic.SetApplicationName(applicationName);
            global::NewRelic.Api.Agent.NewRelic.StartAgent();
        }

        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            await Task.Run(() =>
                {
                    foreach (var logEvent in events)
                    {
                        try
                        {
                            // Made up standard for transactions Property = TransactionName, Value = category::name
                            if (logEvent.IsTransactionEvent())
                            {
                                var transaction = logEvent.Properties.First(x => x.Key == PropertyNameConstants.TransactionName);
                                var transactionValue = transaction.Value.ToString().Replace("\"", "");
                                var transactionValues = transactionValue.Split(new[]
                                {
                                    "::"
                                }, StringSplitOptions.None);

                                if (transactionValues.Length < 2)
                                {
                                    continue;
                                }

                                var category = transactionValues[0].ToNewRelicSafeString();
                                var name = transactionValues[1].ToNewRelicSafeString();

                                global::NewRelic.Api.Agent.NewRelic.SetTransactionName(category, name);
                            }

                            if (logEvent.IsTimerEvent())
                            {
                                EmitResponseTimeMetric(logEvent);
                            }
                            else if (logEvent.IsCounterEvent())
                            {
                                EmitCounterIncrement(logEvent);
                            }
                            else if (logEvent.IsGaugeEvent())
                            {
                                EmitMetric(logEvent);
                            }
                            else if (logEvent.Level == LogEventLevel.Error)
                            {
                                EmitError(logEvent);
                            }
                            else
                            {
                                EmitCustomEvent(logEvent);
                            }
                        }
                        catch (Exception ex)
                        {
                            SelfLog.WriteLine("Event at {0} with message template {1} could not be sent to NewRelic and was be dropped: {2}",
                                              logEvent.Timestamp.ToString("o"), logEvent.MessageTemplate.Text, ex);
                        }
                    }

                })
                .ConfigureAwait(false);
        }

        private static void EmitResponseTimeMetric(LogEvent logEvent)
        {
            // Ignore the Beginning Operation
            if (logEvent.Properties.All(x => x.Key != PropertyNameConstants.TimedOperationElapsedInMs))
            {
                return;
            }

            var elapsedTime = logEvent.Properties.First(x => x.Key == PropertyNameConstants.TimedOperationElapsedInMs);
            var operation = logEvent.Properties.First(x => x.Key == PropertyNameConstants.TimedOperationDescription);

            long numeric;
            if (long.TryParse(elapsedTime.Value.ToString(), out numeric))
            {
                var safeOperationString = operation.Value.ToString().ToNewRelicSafeString();
                global::NewRelic.Api.Agent.NewRelic.RecordResponseTimeMetric(safeOperationString, numeric);
            }
        }

        private static void EmitCounterIncrement(LogEvent logEvent)
        {
            var operation = logEvent.Properties.First(x => x.Key == PropertyNameConstants.CounterName);
            var safeOperationString = operation.Value.ToString().ToNewRelicSafeString();
            global::NewRelic.Api.Agent.NewRelic.IncrementCounter(safeOperationString);
        }

        private static void EmitMetric(LogEvent logEvent)
        {
            var elapsedTime = logEvent.Properties.First(x => x.Key == PropertyNameConstants.GaugeValue);
            
            float numeric;
            if (float.TryParse(elapsedTime.Value.ToString(), out numeric))
            {
                var operation = logEvent.Properties.First(x => x.Key == PropertyNameConstants.GaugeName);
                var safeOperationString = operation.Value.ToString().ToNewRelicSafeString();
                global::NewRelic.Api.Agent.NewRelic.RecordMetric(safeOperationString, numeric);
            }
        }

        private void EmitError(LogEvent logEvent)
        {
            var properties = LogEventPropertiesToNewRelicExceptionProperties(logEvent);
            
            if (logEvent.Exception != null)
            {
                global::NewRelic.Api.Agent.NewRelic.NoticeError(logEvent.Exception, properties);
            }
            else
            {
                var renderedMessage = logEvent.RenderMessage(FormatProvider).ToNewRelicSafeString();
                global::NewRelic.Api.Agent.NewRelic.NoticeError(renderedMessage, properties);
            }
        }

        private void EmitCustomEvent(LogEvent logEvent)
        {
            var properties = LogEventPropertiesToNewRelicCustomEventProperties(logEvent);
            global::NewRelic.Api.Agent.NewRelic.RecordCustomEvent(CustomEventName, properties);
        }
        
        private static IDictionary<string, string> LogEventPropertiesToNewRelicExceptionProperties(LogEvent logEvent)
        {
            var properties = new Dictionary<string, string>();

            foreach (var source in logEvent.Properties.Where(p => p.Value != null))
            {
                var safeKey = source.Key.ToNewRelicSafeString();
                if (properties.ContainsKey(safeKey))
                {
                    SelfLog.WriteLine("Transformed property {0} from event with message template {1} is already in dictionary", safeKey, logEvent.MessageTemplate.Text);
                    continue;
                }

                properties.Add(safeKey, source.Value.ToString());
            }

            return properties;
        }

        private static IDictionary<string, object> LogEventPropertiesToNewRelicCustomEventProperties(LogEvent logEvent)
        {
            var properties = new Dictionary<string, object>
            {
                { PropertyNameConstants.MessageTemplate, logEvent.MessageTemplate.Text }
            };

            foreach (var source in logEvent.Properties.Where(p => p.Value != null))
            {
                var safeKey = source.Key.ToNewRelicSafeString();
                if (properties.ContainsKey(safeKey))
                {
                    SelfLog.WriteLine("Transformed property {0} from event with message template {1} is already in dictionary", safeKey, logEvent.MessageTemplate.Text);
                    continue;
                }

                bool binary;
                float numeric;
                if (bool.TryParse(source.Value.ToString(), out binary))
                {
                    properties.Add(safeKey, (float)(binary ? 1 : 0));
                }
                else if (float.TryParse(source.Value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out numeric))
                {
                    properties.Add(safeKey, numeric);
                }
                else
                {
                    properties.Add(safeKey, source.Value.ToString());
                }
            }

            return properties;
        }
    }
}
