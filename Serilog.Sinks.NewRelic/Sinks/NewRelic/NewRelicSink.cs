using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.NewRelic.Sinks.NewRelic
{
    internal class NewRelicSink : PeriodicBatchingSink
    {
        LogEventLevel? _minimumAcceptedLevel;

        static readonly TimeSpan RequiredLevelCheckInterval = TimeSpan.FromMinutes(2);
        DateTime _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);

        public const int DefaultBatchPostingLimit = 1000;
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);
        private readonly int _batchPostingLimit;
        private readonly TimeSpan _defaultedPeriod;
        private readonly IFormatProvider _formatProvider;
        private readonly IDictionary<string, string> _reservedWords; 

        public NewRelicSink(string applicationName, int batchSizeLimit, TimeSpan period, IFormatProvider formatProvider = null) : base(batchSizeLimit, period)
        {
            this._batchPostingLimit = batchSizeLimit;
            this._defaultedPeriod = period;
            this._formatProvider = formatProvider;

            global::NewRelic.Api.Agent.NewRelic.SetApplicationName(applicationName);

            _reservedWords = PopulateReservedWords();
        }

        private IDictionary<string, string> PopulateReservedWords()
        {
            var reservedWords = new Dictionary<string,string>();

            reservedWords.Add("add","`add`");
            reservedWords.Add("ago","`ago`"); 
            reservedWords.Add("and", "`and`");
            reservedWords.Add("as", "`as`");
            reservedWords.Add("auto", "`auto`");
            reservedWords.Add("begin", "`begin`");
            reservedWords.Add("begintime", "`begintime`");
            reservedWords.Add("compare", "`compare`");
            reservedWords.Add("day", "`day`");
            reservedWords.Add("days", "`days`");
            reservedWords.Add("end", "`end`");
            reservedWords.Add("endtime", "`endtime`");
            reservedWords.Add("explain", "`explain`");
            reservedWords.Add("facet", "`facet`");
            reservedWords.Add("from", "`from`");
            reservedWords.Add("hour", "`hour`");
            reservedWords.Add("hours", "`hours`");
            reservedWords.Add("in", "`in`");
            reservedWords.Add("is", "`is`");
            reservedWords.Add("like", "`like`");
            reservedWords.Add("limit", "`limit`");
            reservedWords.Add("minute", "`minute`");
            reservedWords.Add("minutes", "`minutes`");
            reservedWords.Add("month", "`month`");
            reservedWords.Add("months", "`months`");
            reservedWords.Add("not", "`not`");
            reservedWords.Add("null", "`null`");
            reservedWords.Add("offset", "`offset`");
            reservedWords.Add("or", "`or`");
            reservedWords.Add("second", "`second`");
            reservedWords.Add("seconds", "`seconds`");
            reservedWords.Add("select", "`select`");
            reservedWords.Add("since", "`since`");
            reservedWords.Add("timeseries", "`timeseries`");
            reservedWords.Add("until", "`until`");
            reservedWords.Add("week", "`week`");
            reservedWords.Add("weeks", "`weeks`");
            reservedWords.Add("where", "`where`");
            reservedWords.Add("with", "`with`");


            return reservedWords;            
        }

        protected override Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);

            //TODO: See if there's a way to determine the level of events accepted into NewRelic for now assume all are.

            _minimumAcceptedLevel = LogEventLevel.Verbose;

            return Task.Run(() =>
            {
                foreach (var logEvent in events)
                {
                    var renderedMessage = logEvent.RenderMessage(_formatProvider).ToNewRelicSafeString(_reservedWords);

                    // Made up standard for transactions Property = TransactionName, Value = category::name

                    if (logEvent.IsTransactionEvent())
                    {
                        
                        var transaction = logEvent.Properties.First(x => x.Key == "TransactionName");
                        var transactionValue = transaction.Value.ToString().Replace("\"","");
                        var transactionValues = transactionValue.Split(new[] { "::" }, StringSplitOptions.None);

                        if (transactionValues.Length < 2)
                        {
                            continue;
                        }

                        var category = transactionValues[0].ToNewRelicSafeString(_reservedWords);
                        var name = transactionValues[1].ToNewRelicSafeString(_reservedWords);

                        global::NewRelic.Api.Agent.NewRelic.SetTransactionName(category, name);
                    }

                    if (logEvent.IsTimerEvent())
                    {
                        // Ignore the Beginning Operation

                        if (logEvent.Properties.All(x => x.Key != "TimedOperationElapsedInMs"))
                        {
                            continue;
                        }

                        var elapsedTime = logEvent.Properties.First(x => x.Key == "TimedOperationElapsedInMs");
                        var operation = logEvent.Properties.First(x => x.Key == "TimedOperationDescription");

                        int numeric;
                        var isNumber = int.TryParse(elapsedTime.Value.ToString(), out numeric);

                        if (isNumber)
                        {
                            var safeOperationString = operation.ToString().ToNewRelicSafeString(_reservedWords);

                            global::NewRelic.Api.Agent.NewRelic.RecordResponseTimeMetric(safeOperationString, numeric);
                        }

                        continue;
                    }

                    if (logEvent.IsCounterEvent())
                    {
                        var operation = logEvent.Properties.First(x => x.Key == "CounterName");

                        var safeOperationString = operation.ToString().ToNewRelicSafeString(_reservedWords);

                        global::NewRelic.Api.Agent.NewRelic.IncrementCounter(safeOperationString);

                        continue;
                    }

                    if (logEvent.IsGaugeEvent())
                    {
                        var elapsedTime = logEvent.Properties.First(x => x.Key == "GaugeValue");
                        var operation = logEvent.Properties.First(x => x.Key == "GaugeName");

                        float numeric;
                        var isNumber = float.TryParse(elapsedTime.Value.ToString(), out numeric);

                        if (isNumber)
                        {
                            var safeOperationString = operation.ToString().ToNewRelicSafeString(_reservedWords);

                            global::NewRelic.Api.Agent.NewRelic.RecordMetric(safeOperationString, numeric);
                        }

                        continue;
                    }

                    if (logEvent.Level == LogEventLevel.Error)
                    {
                        var properties = LogEventPropertiesToNewRelicExceptionProperties(logEvent);

                        if (logEvent.Exception != null)
                        {
                            global::NewRelic.Api.Agent.NewRelic.NoticeError(logEvent.Exception, properties);
                        }
                        else
                        {
                            global::NewRelic.Api.Agent.NewRelic.NoticeError(renderedMessage, properties);
                        }
                    }
                    else
                    {
                        var properties = LogEventPropertiesToNewRelicCustomEventProperties(logEvent);

                        global::NewRelic.Api.Agent.NewRelic.RecordCustomEvent(renderedMessage, properties);
                    }
                }
            });
        }



        // The sink must emit at least one event on startup, and the server be
        // configured to set a specific level, before background level checks will be performed.
        protected override void OnEmptyBatch()
        {
            if (_minimumAcceptedLevel != null &&
                _nextRequiredLevelCheckUtc < DateTime.UtcNow)
            {
                EmitBatch(Enumerable.Empty<LogEvent>());
            }
        }
        protected override bool CanInclude(LogEvent evt)
        {
            return _minimumAcceptedLevel == null ||
                   (int)_minimumAcceptedLevel <= (int)evt.Level;
        }
        private IDictionary<string, string> LogEventPropertiesToNewRelicExceptionProperties(LogEvent logEvent)
        {
            var properties = new Dictionary<string, string>();

            foreach (var source in logEvent.Properties.Where(p => p.Value != null))
            {
                var renderedProperty = source.Value.ToString().ToNewRelicSafeString(_reservedWords);

                properties.Add(source.Key.ToNewRelicSafeString(_reservedWords), renderedProperty);
            }

            return properties;
        }
        private IDictionary<string, object> LogEventPropertiesToNewRelicCustomEventProperties(LogEvent logEvent)
        {
            var properties = new Dictionary<string, object>();

            foreach (var source in logEvent.Properties.Where(p => p.Value != null))
            {
                double numeric;
                var isNumber = double.TryParse(source.Value.ToString(), out numeric);

                var safeKey = source.Key.ToNewRelicSafeString(_reservedWords);

                if (!isNumber)
                {
                    var renderedProperty = source.Value.ToString().ToNewRelicSafeString(_reservedWords);
                    
                    properties.Add(safeKey, renderedProperty);
                }
                else
                {
                    properties.Add(safeKey, (float)numeric);
                }
            }

            return properties;
        }
    }
}