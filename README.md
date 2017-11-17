
A Serilog sink that writes events to the [NewRelic](https://newrelic.com) apm application.

## Getting started

Install NewRelic NuGetPackage

Configure NewRelic Settings:

```xml
  <appSettings>
    <add key="NewRelic.LicenseKey" value="<API Key here>"/>
    <add key="NewRelic.AgentEnabled" value="true"/>
    <add key="NewRelic.AppName" value="Serilog.Sinks.NewRelic.Sample"/>
  </appSettings>
```

Point the logger to NewRelic:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.NewRelic(applicationName: "Serilog.Sinks.NewRelic.Sample", customEventName: "LoggedEvents")
    .CreateLogger();
```

Both parameters are optional.
If 'applicationName' is omitted, then the value of the "NewRelic.AppName" appSetting will be used.
If 'customEventName' is omitted, then the name of the custom transaction is defaulted to "Serilog".

And use the Serilog logging methods to associate named properties with log events:

```csharp
Log.Error("Failed to log on user {ContactId}", contactId);
```

Errors are displayed under the Events-Errors section in NewRelic.
To view Verbose, Debug, Information and Warning events, go to Insights -> Data Explorer -> Events and select the appropriate custom stransaction (default: "Serilog")

The sink also supports sending [Serilog.Metrics](https://github.com/serilog-metrics/serilog-metrics) to NewRelic although this may turn out to be largely redundant!

```csharp
using (logger.BeginTimedOperation("Time a thread sleep for 2 seconds."))
{
    Thread.Sleep(1000);
    using (logger.BeginTimedOperation("And inside we try a Task.Delay for 2 seconds."))
    {
        Task.Delay(2000).Wait();
    }
    Thread.Sleep(1000);
}
```

The Metrics can be viewed and analysed under Insights -> Data Explorer -> Metrics, and then serching the Custom category for your named metric.