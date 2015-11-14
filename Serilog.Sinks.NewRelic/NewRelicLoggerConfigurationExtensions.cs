using System;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.NewRelic.Sinks.NewRelic;

namespace Serilog.Sinks.NewRelic
{
    public static class NewRelicLoggerConfigurationExtensions
    {
        public static LoggerConfiguration NewRelic(
            this LoggerSinkConfiguration loggerSinkConfiguration,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            int batchPostingLimit = NewRelicSink.DefaultBatchPostingLimit,
            TimeSpan? period = null,
            string applicationName = null,
            string bufferBaseFilename = null,
            long? bufferFileSizeLimitBytes = null)
        {
            if (loggerSinkConfiguration == null) throw new ArgumentNullException("loggerSinkConfiguration");

            if (bufferFileSizeLimitBytes.HasValue && bufferFileSizeLimitBytes < 0)
                throw new ArgumentException("Negative value provided; file size limit must be non-negative");

            if (string.IsNullOrEmpty(applicationName))
                throw new ArgumentException("Must supply an application name");

            var defaultedPeriod = period ?? NewRelicSink.DefaultPeriod;

            ILogEventSink sink;

            if (bufferBaseFilename == null)
                sink = new NewRelicSink(applicationName, batchPostingLimit, defaultedPeriod);
            else
            {
                //sink = new DurableNewRelicSink(bufferBaseFilename, applicationName, batchPostingLimit, defaultedPeriod,
                //    bufferFileSizeLimitBytes);

                throw new NotImplementedException("DurableNewRelicSink is not implemented yet.");
            }

            return loggerSinkConfiguration.Sink(sink, restrictedToMinimumLevel);
        }
    }
}