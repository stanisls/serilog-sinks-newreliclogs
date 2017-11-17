using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog.Events;

namespace Serilog.Sinks.NewRelic
{
    public static class LogEventExtensions
    {
        private static readonly IDictionary<string, string> ReservedWords =
            new Dictionary<string, string>
            {
                { "add", "`add`" },
                { "ago", "`ago`" },
                { "and", "`and`" },
                { "as", "`as`" },
                { "auto", "`auto`" },
                { "begin", "`begin`" },
                { "begintime", "`begintime`" },
                { "compare", "`compare`" },
                { "day", "`day`" },
                { "days", "`days`" },
                { "end", "`end`" },
                { "endtime", "`endtime`" },
                { "explain", "`explain`" },
                { "facet", "`facet`" },
                { "from", "`from`" },
                { "hour", "`hour`" },
                { "hours", "`hours`" },
                { "in", "`in`" },
                { "is", "`is`" },
                { "like", "`like`" },
                { "limit", "`limit`" },
                { "minute", "`minute`" },
                { "minutes", "`minutes`" },
                { "month", "`month`" },
                { "months", "`months`" },
                { "not", "`not`" },
                { "null", "`null`" },
                { "offset", "`offset`" },
                { "or", "`or`" },
                { "second", "`second`" },
                { "seconds", "`seconds`" },
                { "select", "`select`" },
                { "since", "`since`" },
                { "timeseries", "`timeseries`" },
                { "until", "`until`" },
                { "week", "`week`" },
                { "weeks", "`weeks`" },
                { "where", "`where`" },
                { "with", "`with`" }
            };

        public static bool IsTimerEvent(this LogEvent logEvent)
        {
            return logEvent.Properties.Any(p => p.Key == PropertyNameConstants.TimedOperationId);
        }

        public static bool IsCounterEvent(this LogEvent logEvent)
        {
            return logEvent.Properties.Any(p => p.Key == PropertyNameConstants.CounterName);
        }

        public static bool IsGaugeEvent(this LogEvent logEvent)
        {
            return logEvent.Properties.Any(p => p.Key == PropertyNameConstants.GaugeName);
        }

        public static bool IsTransactionEvent(this LogEvent logEvent)
        {
            return logEvent.Properties.Any(p => p.Key == PropertyNameConstants.TransactionName);
        }

        public static string ToNewRelicSafeString(this string str)
        {
            return ReservedWords.Aggregate(str,
                (current, reservedWord) => current.ReplaceCaseInsensitiveFind(reservedWord.Key, reservedWord.Value));
        }

        public static string ReplaceCaseInsensitiveFind(this string str, string currValue,string newValue)
        {
            var protectedWords =  Regex.Replace(str, 
                "\\b" + Regex.Escape(currValue) + "\\b",
                newValue,
                RegexOptions.IgnoreCase);

            var safeCharacters = Regex.Replace(protectedWords, @"[^a-zA-Z0-9:_\.\- ]", "");

            return safeCharacters;
        }
    }
}