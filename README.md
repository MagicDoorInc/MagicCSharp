# MagicCSharp

A set of C# packages for the parts of an application that are not your business logic — use cases,
repositories, events, a testable clock, sortable ids, background jobs, RFC 7807 errors — and a repository
layout they were built for.

**Both halves work without the other.** Add one package to a project you already have and the rest of it
does not change: use cases register themselves, repositories give you a filter-to-SQL layer, `FakeClock`
makes time-dependent code testable. That is how most people start, and it is a small commitment.

The layout is the other half, for when you are starting something or restructuring it. One deployable
service; inside it, a tree of domains, each owning its use cases, entities, endpoints and tests. The shape is
**decided** — where a class goes, what it may reference, what its assembly is called — **enforced**, because
a reference pointing the wrong way fails the build and `mcs validate` fails CI, and **generated**, so nobody
has to remember it. A service that reads the same at a hundred use cases as it did at ten.

Start wherever you like. The packages do not ask for the layout, and the layout is what makes them add up to
something.

## Use cases

A use case is one business operation, as a plain class. No base class, no framework types in the signature,
and nothing to register:

```csharp
public record PlaceOrderRequest(long CustomerId, decimal Total);

public interface IPlaceOrderUseCase : IMagicUseCase
{
    Task<Order> Execute(PlaceOrderRequest request);
}

public class PlaceOrderUseCase(IOrdersRepository orders, IEventDispatcher events) : IPlaceOrderUseCase
{
    public async Task<Order> Execute(PlaceOrderRequest request)
    {
        var order = await orders.Create(new OrderEdit
        {
            CustomerId = request.CustomerId,
            Total = request.Total,
            Status = OrderStatus.Pending,
        });

        events.Dispatch(new OrderPlacedEvent { OrderId = order.Id, CustomerId = order.CustomerId });

        return order;
    }
}
```

```csharp
builder.Services.AddMagicCSharp();
```

That one call finds every implementation of `IMagicUseCase` and registers it under its own interface, so a
controller takes `IPlaceOrderUseCase` in its constructor and there is no wiring to write or to forget. Two
implementations of one interface is a startup error naming both, rather than whichever reflection returned
first. `[MagicUseCase(ServiceLifetime.Singleton)]` changes the lifetime when the default scoped is wrong. An
interface extending the marker with no implementation is also a startup error, rather than a resolution
failure on the first request that needs it.

The interface is the point. It is what a controller depends on, what a test replaces, and what stops the
next caller reaching past the operation into the repository underneath.

### Which makes them testable without a host

No web server, no database, no mocking framework — the dependencies are interfaces, so a test constructs the
thing directly:

```csharp
var useCase = new PlaceOrderUseCase(new FakeOrdersRepository(), new RecordingEventDispatcher());

var order = await useCase.Execute(new PlaceOrderRequest(CustomerId: 7, Total: 42.50m));

Assert.Equal(OrderStatus.Pending, order.Status);
```

Both fakes are a few lines you write, in the test project, implementing the interface the use case asked
for — there is no framework double to learn. The
[example project](https://github.com/MagicDoorInc/MagicCSharp-ExampleProject) has both in full.

`MagicCSharp.Testing` supplies doubles for the framework's own seams — most usefully a clock you move by
hand, so anything time-dependent is testable in milliseconds rather than by waiting:

```csharp
clock.SetTime(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
await createLease.Execute(request);

clock.AdvanceDays(31);
await applyLateFees.Execute();          // now asserts against a month later
```

Code that reads `DateTime.Now` cannot do this, which is why `mcs validate` refuses it.

## What else is in the box

**Repositories** that turn a filter object into SQL, with pagination, soft delete and a search column, over
Entity Framework and PostgreSQL. Entities live in your domain; the DAL and the EF attributes live behind
`Data.Models`, which has no EF dependency at all, so the arrow points from storage toward the domain and
never back.

**Events** with in-process, Kafka and SQS transports behind one `IEventDispatcher`. Handlers are discovered
the same way use cases are. Dispatch is fire-and-forget on every transport — deliberately, so that moving
from in-process to Kafka does not change how a handler behaves.

**Snowflake ids**, assigned before insert, so a caller knows an entity's id without a round trip and ids
stay unique across instances without coordination.

**Background jobs** that schedule from the clock rather than from how long the last run took, so they do not
drift.

**RFC 7807 errors.** A repository throwing `NotFoundException` for a row that is not there reaches the
caller as a 404 with a problem+json body, not a 500 with a stack trace — and outside Development the detail
is logged rather than returned.

### The packages

Fourteen, split so you take only what you use — plus `MagicCSharp.App`, which bundles the four a web service
needs when you would rather not choose.

| Package | Add it when you want | Brings with it |
|---|---|---|
| **[MagicCSharp.App](src/MagicCSharp.App/)** | A web service wired in two calls | the four below it |
| **[MagicCSharp](src/MagicCSharp/)** | Use cases, `IClock`, Snowflake ids, request IDs, `Optional<T>` | DI + logging abstractions, IdGen |
| **[MagicCSharp.AspNetCore](src/MagicCSharp.AspNetCore/)** | Request-ID middleware, RFC 7807 error handling, startup preflight | the ASP.NET shared framework |
| **[MagicCSharp.Scheduling](src/MagicCSharp.Scheduling/)** | Drift-free background jobs | DistributedLock, hosting |
| **[MagicCSharp.Data](src/MagicCSharp.Data/)** | Repository contracts, pagination, LINQ filter helpers | nothing — no persistence library |
| **[MagicCSharp.Data.EntityFramework](src/MagicCSharp.Data.EntityFramework/)** | The repository base classes and DALs | EF Core |
| **[MagicCSharp.Data.Postgres](src/MagicCSharp.Data.Postgres/)** | Pooled context factory, design-time factory, UTC interceptor | Npgsql |
| **[MagicCSharp.Events](src/MagicCSharp.Events/)** | Event dispatch and handler discovery | System.Text.Json |
| **[MagicCSharp.Events.Kafka](src/MagicCSharp.Events.Kafka/)** | Kafka transport | Confluent.Kafka |
| **[MagicCSharp.Events.SQS](src/MagicCSharp.Events.SQS/)** | SQS transport | AWSSDK.SQS |
| **[MagicCSharp.Testing](src/MagicCSharp.Testing/)** | `FakeClock`, `FakeKeyGen`, in-memory locks | DistributedLock |
| **[MagicCSharp.Testing.Database](src/MagicCSharp.Testing.Database/)** | Repository tests against real PostgreSQL | Testcontainers, xUnit |
| **[MagicCSharp.Cli](src/MagicCSharp.Cli/)** | The `mcs` tool | installed globally, not referenced |
| **[MagicCSharp.Templates](templates/)** | `dotnet new magiccsharp-repo` | installed as a template pack |

A domain project referencing `MagicCSharp.Data` gets the repository interfaces and no persistence library at
all — which is the point of the split. Wanting `FakeClock` does not mean wanting Docker.

## A web service in two calls

If you want the whole set rather than a piece of it:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddMagicApp();

var app = builder.Build();
app.UseMagicApp(builder);
app.Run();
```

That registers use cases, `IClock`, Snowflake ids, request IDs, in-process events, scheduling defaults and
problem-details error handling, then builds the pipeline in the order those need. Every call it makes is
public on the package that owns it, so outgrowing the defaults means replacing two lines with five rather
than working around a framework. [How, and what each option does →](src/MagicCSharp.App/)

---

# The layout

Everything above works in any project, arranged however you like. This is the arrangement it was built for,
and what it is for is a service whose domain keeps growing without becoming unreadable.

## The shape

```
Apps/Shop/
  Shop.App/                      Program.cs — a list of references and little else
  Shop.Domains/Orders/
    Default/                     use cases, event handlers
    Models/                      entities, edits, filters
    App/                         this domain's controllers
    Tests/
    Fulfilment/                  a subdomain: the same shape, one level down
  Data/
    Data.Models/                 repository interfaces — no EF dependency
    Data.EntityFramework/        DALs, repositories, context, migrations
Libs/                            what more than one service uses
```

A domain grows by gaining siblings, not by getting wider. Each one brings its own endpoints, so `Shop.App`
never becomes the folder where every feature's controllers pile up.

## Sixty seconds

```bash
dotnet tool install -g MagicCSharp.Cli
docker run -d --name shop-db -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17

mcs init --prefix Acme
mcs create-app --name Shop --database shop
mcs create-domain --solution Shop --name Orders --models --tests
mcs create-domain --solution Shop --name Orders.App
mcs add-entity --solution Shop --domain Orders --name Order --paginated

dotnet run --project Apps/Shop/Shop.App
curl localhost:5200/hello
```

That is a service with a domain in it: a use-case project the host references, an `Order` entity across four
files in three projects that agree about names and namespaces, its repository registered, and a place for
the domain's controllers. Re-run any of those commands and nothing changes — an existing file is reported
and skipped, never overwritten.

The service needs the database because `create-app --database` wires one in. Leave `--database` off for a
service that has none, or set `DB_VERIFY_CONNECTION=false` to let it boot without one.

## What the shape buys

**You stop deciding where things go.** Four questions get answered once, by the layout, instead of every
time by whoever is there that week:

| You are writing | It goes in |
|---|---|
| Code two or more services use — an event contract, a typed client | a shared library, `Libs/Events/` |
| A business area with its own entities, rules and use cases | a domain, `Apps/Shop/Shop.Domains/Orders/` |
| That domain's endpoints — controllers, request and response types | the domain's App, `.../Orders/App/` |
| Code one service uses from several domains, that is not itself a domain | an app library, `Apps/Shop/Shop.Processors/` |

**The build enforces it.** `Data.Models` holds interfaces and does not reference Entity Framework, so the
arrow points from storage toward the domain and never back. A subdomain may depend on its parent; the
reverse is a cycle and the compiler says so. You can read a domain without reading a single EF attribute.

**`mcs validate` catches what still compiles.** A `DateTime.Now` that makes behaviour untestable. An event
carrying an entity, which will deserialize to nulls after a deploy. It exits non-zero, so CI holds the line
instead of a reviewer.

**Every entity looks the same.** `mcs add-entity` writes the four files an entity needs — across three
projects that each have to agree about names, namespaces and generic arguments — and registers it. That is
not typing saved so much as a class of mistake removed, and it means anyone can open any service and
recognise what they are looking at.

**It holds when the domain gets big.** MagicDoor's insurance service is one deployable with a generic
`Insurance` domain and three provider subdomains beneath it — Sure, DamageWaiver, ExternalInsurance — nearly
ninety use cases and eleven controllers. Each provider brings its own endpoints, models and tests. No type in
the parent's contract project names a provider, and its logic names one in a single statistics use case. To
add a fourth provider you add a directory, not a service, and you do not open the other three.

**One repository, several services, no version dance.** A change spanning two services is one commit, not a
package publish and a wait. But you still build one service at a time: each has its own `.slnx`, and
`Acme.All.slnx` is regenerated from disk for the times you need everything.

**[The full guide →](docs/repository-layout.md)**

## Requirements

The .NET 10 SDK. The libraries target net9.0; the CLI, the tests and generated repositories target net10.0.
PostgreSQL for the data packages. Docker only for `MagicCSharp.Testing.Database`.

## Going further

- **[The repository layout](docs/repository-layout.md)** — the full guide: domains, subdomains, app
  libraries, entities, and what the tool wires versus what it leaves you.
- **[Template overrides](docs/template-overrides.md)** — every file `mcs` generates comes from a template
  you can replace, one file at a time, keeping the rest. Teams put theirs in a repository of their own.
- **[The `mcs` reference](src/MagicCSharp.Cli/)** — every command and what it does.
- **[The example project](https://github.com/MagicDoorInc/MagicCSharp-ExampleProject)** — two services, a
  domain with a subdomain, a shared event contract, and tests at three levels.
- **[CHANGELOG](CHANGELOG.md)** — including how to migrate across a breaking version.

## Where it comes from

This is the layout MagicDoor's backend is built on, extracted so it can be used outside it, and shaped by
building systems at Amazon and Disney before that. The opinions are not theoretical — they are what was left
after finding out which structures survive a codebase getting large and a team changing.

That is offered as an explanation of why the decisions look like this, not as a reason to trust them. Where
a decision has a cost, the cost is written next to it. Judge them on that.

## Contributing

The framework is small on purpose — issues, template changes and new `validate` rules are all welcome.
[CONTRIBUTING.md](CONTRIBUTING.md) covers building it, running the tests without Docker, and what to raise
before writing code. [GOVERNANCE.md](GOVERNANCE.md) says who maintains it and what `0.x` means for breaking
changes. To report a vulnerability see [SECURITY.md](SECURITY.md) — please do not open a public issue.

## License

MIT. See [LICENSE](LICENSE).
