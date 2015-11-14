using System;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.RollingFile;

namespace Serilog.Sinks.NewRelic.Sinks.NewRelic
{
    internal class DurableNewRelicSink : ILogEventSink, IDisposable
    {
        private readonly NewRelicLogShipper _shipper;
        private readonly RollingFileSink _sink;
        private string bufferBaseFilename;
        private string applicationName;
        private int batchPostingLimit;
        private TimeSpan defaultedPeriod;
        private long? bufferFileSizeLimitBytes;

        public DurableNewRelicSink(string bufferBaseFilename, string applicationName, int batchPostingLimit, TimeSpan defaultedPeriod, long? bufferFileSizeLimitBytes)
        {
            this.bufferBaseFilename = bufferBaseFilename;
            this.applicationName = applicationName;
            this.batchPostingLimit = batchPostingLimit;
            this.defaultedPeriod = defaultedPeriod;
            this.bufferFileSizeLimitBytes = bufferFileSizeLimitBytes;
        }

        public void Emit(LogEvent logEvent)
        {
            // This is a lagging indicator, but the network bandwidth usage benefits
            // are worth the ambiguity.
            var minimumAcceptedLevel = _shipper.MinimumAcceptedLevel;
            if (minimumAcceptedLevel == null ||
                (int)minimumAcceptedLevel <= (int)logEvent.Level)
            {
                _sink.Emit(logEvent);
            }
        }

        public void Dispose()
        {
            _sink.Dispose();
            _shipper.Dispose();
        }
    }
}