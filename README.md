
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
    .WriteTo.NewRelic(applicationName: "Serilog.Sinks.NewRelic.Sample")
    .CreateLogger();
```

And use the Serilog logging methods to associate named properties with log events:

```csharp
Log.Error("Failed to log on user {ContactId}", contactId);
```

The sink also supports sending Serilog.Metrics to NewRelic althought this requires a custom transaction in NewRelic See [here](https://docs.newrelic.com/docs/agents/net-agent/instrumentation/net-custom-instrumentation) and may turn out to be largely redundant!

```csharp
// Adding a custom transaction

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