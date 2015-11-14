using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog.Events;

namespace Serilog.Sinks.NewRelic.Sinks.NewRelic
{
    public static class LogEventExtensions
    {
        public static bool IsTimerEvent(this LogEvent logEvent)
        {
            return logEvent.Properties.Any(p => p.Key == "TimedOperationId");
        }

        public static bool IsCounterEvent(this LogEvent logEvent)
        {
            return logEvent.Properties.Any(p => p.Key == "CounterName");
        }

        public static bool IsGaugeEvent(this LogEvent logEvent)
        {
            return logEvent.Properties.Any(p => p.Key == "GaugeName");
        }

        public static bool IsTransactionEvent(this LogEvent logEvent)
        {
            return logEvent.Properties.Any(p => p.Key == "TransactionName");
        }

        public static string ToNewRelicSafeString(this string str, IDictionary<string, string> reservedWords)
        {
            return reservedWords.Aggregate(str,
                (current, reservedWord) => current.ReplaceCaseInsensitiveFind(reservedWord.Key, reservedWord.Value));
        }

        public static string ReplaceCaseInsensitiveFind(this string str, string currValue,string newValue)
        {
            var protectedWords =  Regex.Replace(str, 
                "\\b" + Regex.Escape(currValue) + "\\b",
                newValue,
                RegexOptions.IgnoreCase);

            var safeCharacters = Regex.Replace(protectedWords, @"[^a-zA-Z0-9:_ ]", "");

            return safeCharacters;
        }
    }
}