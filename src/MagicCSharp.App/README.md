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

## A startup class

The same two calls, with the host's lines in one place you override instead of copy:

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        MagicApp.Run<BoilersStartup>(args);
    }
}

public class BoilersStartup : MagicAppStartup
{
    protected override void AddAppServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<BoilerRegistry>();
    }

    protected override void UseAppPipeline(WebApplication app)
    {
        app.MapGet("/boilers/{boilerId}", (long boilerId) =>
        {
            throw new NotFoundException("Boiler");
        });
    }
}
```

`MagicApp.Run` builds the app, logs `Application started in {ElapsedMilliseconds}ms`, and runs it. A start-up failure
goes to `OnFatal` (standard error by default; an `AggregateException`'s inner one) and is rethrown, so the process
exits non-zero. `MagicApp.Build<TStartup>(args)` returns the app ready to run, for a test or a host of your own.

| Override | Default | |
|---|---|---|
| `AddAppServices` | (you write it) | the app's repositories, clients and modules |
| `UseAppPipeline` | nothing | the app's own endpoints and middleware, after the framework's |
| `Options` | `new MagicAppOptions()` | set it in the constructor, as [below](#changing-the-defaults) |
| `AddFrameworkServices` | `builder.AddMagicApp(Options)` | a codebase with its own hosting, once, in a base its apps share; call `base` after registering Kafka or SQS |
| `UseFrameworkPipeline` | `app.UseMagicApp(builder, Options)` | the same |
| `OnStarted` | the start-up log line | your own |
| `OnFatal` | standard error | your own logging |

**When to write `Main` by hand:** a host that is not a web application (a worker, a console tool), or one whose
start-up the steps above cannot express. The two calls at the top of this page are the same thing, unwrapped.

## The assembly loader

`AddMagicApp` loads the application's assemblies itself before it scans for use cases and handlers, so a service
on it needs nothing more. A host whose discovery only looks at the assemblies already loaded — a codebase's own
scan — needs each referenced project loaded first. With [MagicCSharp.Generators](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Generators/README.md),
set one property in the host project:

```xml
<PropertyGroup>
  <MagicCSharpGenerateAssemblyLoader>true</MagicCSharpGenerateAssemblyLoader>
</PropertyGroup>
```

The compiler writes `{RootNamespace}.AssemblyLoader`, a module initializer that loads every project the host
references, transitive ones included, before `Main`; a project added later is in it on the next build. Package
references are not loaded. Calling `AssemblyLoader.Load()` again, from a test fixture, is harmless. A project that
already declares `AssemblyLoader` fails the build (MCS1609): delete the hand-written one, or leave the property off to
keep it.

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
    ShouldMapControllers = false,        // minimal APIs
    ShouldRegisterScheduling = false,    // more than one instance — register your own store and lock provider
    KeyGeneratorId = 3,                  // stable per instance, rather than random
});
```

| Option | Default | |
|---|---|---|
| `ShouldRegisterEvents` | on | Handler discovery, plus in-process dispatch unless something already claimed `IEventDispatcher` |
| `ShouldUseOpenTelemetryMetrics` | off | Event metrics through OpenTelemetry rather than discarded |
| `ShouldRegisterScheduling` | on | The single-machine schedule store and file-system lock |
| `LockDirectory` | temp | Where the file lock provider writes |
| `ShouldHandleErrors` | on | Exceptions to RFC 7807 responses |
| `ShouldMapControllers` | on | `AddControllers` and `MapControllers` |
| `ShouldApplyJsonConventions` | on | Enums as their names and `Optional<T>` round-tripping, for controllers and minimal APIs alike |
| `ShouldRunPreflight` | on | Resolve every registration at startup — needs the `builder` passed to `UseMagicApp` |
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
