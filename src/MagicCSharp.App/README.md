# MagicCSharp.App

Everything a MagicCSharp web service needs, in two calls.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddMagicApp();

var app = builder.Build();
app.UseMagicApp(builder);
app.Run();
```

That registers use cases and their dependencies, `IClock`, Snowflake IDs, request-ID tracking, in-process
events, single-machine scheduling defaults, problem-details error handling and controllers — then builds the
pipeline in the order those need, and resolves every registration once at startup so a miswired dependency
fails the deploy rather than the first request.

## What it brings in

`MagicCSharp`, `MagicCSharp.AspNetCore`, `MagicCSharp.Events`, `MagicCSharp.Scheduling`.

**This is a shortcut, not a layer.** Every call it makes is public on the package that owns it, so outgrowing
the defaults means replacing this with the five lines it stands for — not working around it.

Use the individual packages when you want to choose each piece, and for anything that is not a web service:
a worker, a console app, a test project. A project that only needs `IClock` should reference `MagicCSharp`,
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
| `Preflight` | on | Resolve every registration at startup |
| `KeyGeneratorId` | random | Snowflake generator id, 0-1023 |
| `AssemblyFilter` | non-framework | Which assemblies are scanned for use cases |

Pass the same options to both calls.

## Kafka or SQS instead of in-process events

Register the transport before `AddMagicApp`, and it is left alone:

```csharp
builder.Services.RegisterMagicKafkaEvents(kafkaConfig);
builder.AddMagicApp();
```

Same for scheduling: register your own `IScheduleStore` and `IDistributedLockProvider` first and the
single-machine defaults step aside.
