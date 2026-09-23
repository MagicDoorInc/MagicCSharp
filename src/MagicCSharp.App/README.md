# MagicCSharp.App

Everything a MagicCSharp web service needs, in two calls. Add it when you are starting a web service and want
the framework's defaults without choosing each piece; the day you need to choose, replace it with the calls
it stands for.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddMagicApp();

var app = builder.Build();
app.UseMagicApp(builder);
app.Run();
```

`AddMagicApp` registers use cases and their dependencies, `TimeProvider`, Snowflake ids, request-id tracking,
in-process events, single-machine scheduling defaults, problem-details error handling, controllers and the
JSON conventions the framework's own types need. `UseMagicApp` resolves every registration once — so a
miswired dependency fails the deploy rather than the first request — then builds the pipeline in the order
those pieces need: request id, error handling, controllers.

## What it brings in

`MagicCSharp`, `MagicCSharp.AspNetCore`, `MagicCSharp.Events`, `MagicCSharp.Scheduling`.

**This is a shortcut, not a layer.** Every call it makes is public on the package that owns it, so outgrowing
the defaults means replacing this with the five lines it stands for — not working around it.

Use the individual packages when you want to choose each piece, and for anything that is not a web service:
a worker, a console app, a test project. A project that only needs use cases should reference `MagicCSharp`,
not this.

## Changing the defaults

```csharp
builder.AddMagicApp(new MagicAppOptions
{
    Controllers = false,        // minimal APIs
    Scheduling = false,         // more than one instance — register your own store and lock provider
    KeyGeneratorId = 3,         // stable per instance, rather than random
});
```

| Option | Default | |
|---|---|---|
| `Events` | on | Handler discovery, plus in-process dispatch unless something already claimed `IEventDispatcher` |
| `OpenTelemetryMetrics` | off | Event metrics through OpenTelemetry rather than discarded |
| `Scheduling` | on | The single-machine schedule store and file-system lock |
| `LockDirectory` | temp | Where the file lock provider writes |
| `ErrorHandling` | on | Exceptions to RFC 7807 responses |
| `Controllers` | on | `AddControllers` and `MapControllers` |
| `JsonConventions` | on | Enums as their names and `Optional<T>` round-tripping, for controllers and minimal APIs alike |
| `Preflight` | on | Resolve every registration at startup — needs the `builder` passed to `UseMagicApp` |
| `KeyGeneratorId` | random | Snowflake generator id, 0–1023 |
| `AssemblyFilter` | non-framework | Which assemblies are scanned for use cases and handlers |

Pass the same options to both calls.

## Kafka or SQS instead of in-process events

Register the transport before `AddMagicApp`, and it is left alone:

```csharp
builder.Services.AddMagicKafkaEvents(kafkaConfig);
builder.AddMagicApp();
```

Same for scheduling: register your own `IScheduleStore` and `IDistributedLockProvider` first and the
single-machine defaults step aside.

## The packages behind it

- [MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp/README.md)
  — use cases, ids, request ids, `TimeProvider` registration
- [MagicCSharp.AspNetCore](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.AspNetCore/README.md)
  — request-id middleware, error handling, preflight
- [MagicCSharp.Events](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Events/README.md)
  — `IEventDispatcher` and handlers
- [MagicCSharp.Scheduling](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Scheduling/README.md)
  — drift-free background jobs

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
