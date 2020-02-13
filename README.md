
A Serilog sink that writes events to the [NewRelic Logs](https://docs.newrelic.com/docs/logs/new-relic-logs/get-started/introduction-new-relic-logs).

## Getting started

Optionally configure NewRelic settings:

```xml
  <appSettings>
    <add key="NewRelic.AppName" value="Serilog.Sinks.NewRelic.Sample"/>
  </appSettings>
```

Point the logger to NewRelic Logs:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.NewRelicLogs(endpointUrl: "https://log-api.newrelic.com/log/v1", applicationName: "Serilog.Sinks.NewRelic.Sample", licenseKey: "[Your API key]")
    .CreateLogger();
```
The available parameters are:
* 'applicationName' of the current application in NewRelic If the parameter is omitted, then the value of the "NewRelic.AppName" appSetting will be used.
* 'endpointUrl' is the ingestion URL of NewRelic Logs. The US endpoint is used by default if this value is omitted.
* 'licenseKey' is the NewRelic License key, which is also used with the NewRelic Agent.
* 'insertKey' is New Relic Insert API key. Either 'licenseKey' or 'insertKey' must be supplied.

The events are submitted to NewRelic Logs in batches, and the sink is derived from [PeriodicBatchingSink](https://github.com/serilog/serilog-sinks-periodicbatching).
It therefore supports 'batchSizeLimit' and 'period' parameters of its base. The batches are formatted using NewRelic Logs [detailed JSON body](https://docs.newrelic.com/docs/logs/new-relic-logs/log-api/introduction-log-api#json-content) and are transmitted compressed.

All properties along with the rendered message will be emitted to NewRelic Logs.
This sink adds four additional properties:
* 'timestamp' in milliseconds since epoch
* 'application' holds the value from 'applicationName' or from 'NewRelic.AppName' appSetting
* 'level' is the actual log level of the event.
* 'stack_trace' holds the stack trace portion of an exception.
