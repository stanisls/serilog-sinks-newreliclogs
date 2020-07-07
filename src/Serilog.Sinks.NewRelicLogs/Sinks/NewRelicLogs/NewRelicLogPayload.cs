using System.Collections.Generic;

namespace Serilog.Sinks.NewRelicLogs
{
    public class NewRelicLogPayload
    {
        public NewRelicLogCommon common { get; set; } = new NewRelicLogCommon();

        public IList<NewRelicLogItem> logs { get; set; } = new List<NewRelicLogItem>();
    }

    public class NewRelicLogCommon
    {
        public IDictionary<string, object> attributes { get; set; } = new Dictionary<string, object>();
    }

    public class NewRelicLogItem
    {
        public long timestamp { get; set; }

        public string message { get; set; }

        public IDictionary<string, object> attributes { get; set; } = new Dictionary<string, object>();
    }
}
