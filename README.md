
A Serilog sink that writes events to the [NewRelic](https://newrelic.com) apm application.

[![Package Logo](http://serilog.net/images/serilog-sink-seq-nuget.png)](http://nuget.org/packages/serilog.sinks.seq)

## Getting started

To get started install the _Serilog.Sinks.Seq_ package from Visual Studio's _NuGet_ console:

```powershell
PM> Install-Package Serilog.Sinks.Seq
```

Point the logger to NewRelic:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.NewRelic()
    .CreateLogger();
```

And use the Serilog logging methods to associate named properties with log events:

```csharp
Log.Error("Failed to log on user {ContactId}", contactId);
```
