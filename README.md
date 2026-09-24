<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/banner-dark.svg">
    <img src="docs/assets/banner-light.svg" alt="MagicCSharp — business logic as small use cases you chain together" width="100%">
  </picture>
</p>

<p align="center">
  <a href="https://github.com/MagicDoorInc/MagicCSharp/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/MagicDoorInc/MagicCSharp/ci.yml?branch=master&style=flat-square&label=ci" alt="CI"></a>
  <a href="https://www.nuget.org/packages/MagicCSharp"><img src="https://img.shields.io/nuget/v/MagicCSharp?style=flat-square&label=nuget" alt="NuGet version"></a>
  <a href="https://www.nuget.org/profiles/MagicDoor"><img src="https://img.shields.io/nuget/dt/MagicCSharp?style=flat-square&label=downloads" alt="NuGet downloads"></a>
  <img src="https://img.shields.io/badge/.NET-9%20%7C%2010-512BD4?style=flat-square" alt=".NET 9 | 10">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="License: MIT"></a>
</p>

# MagicCSharp

**Business logic as small use cases you chain together — not a giant service class.** `PlaceOrder` is a class.
`AttachPayment` is another. Big work is those classes called in order, and everything a service needs around
them — repositories, events, background services, errors, test doubles, scaffolding and the house style as
build errors — comes in packages you take one at a time.

It is how every C# service at [MagicDoor](https://magicdoor.com), [Revoco](https://revoco.ai) and
[AgentParley](https://agentparley.ai) is written, designed by engineers from Amazon and Google. MIT.

[Quick start](#quick-start) · [Example](examples/PropertyManagement/) · [Built for AI agents](#small-operations-are-what-an-ai-agent-needs) · [Packages](#the-packages) · [Layout guide](docs/repository-layout.md) · [Changelog](CHANGELOG.md)

<table>
<tr><td><b>Small use cases</b></td><td>One business operation per class, found and registered by one call. The next feature is another class, not another method on a 2,000-line service.</td></tr>
<tr><td><b>Built for AI agents</b></td><td>An operation's scope is its class and its blast radius is its interface. <code>mcs init</code> writes <code>AGENTS.md</code> and the conventions for Claude Code, Codex, Cursor and the rest.</td></tr>
<tr><td><b>The house style is a build error</b></td><td><code>MagicCSharp.Analyzers</code> makes <code>DateTime.Now</code>, positional records, missing braces and names like <code>useCase</code> fail the build — for people and agents alike.</td></tr>
<tr><td><b>Events on any transport</b></td><td>Publish with one interface; run the handlers in-process, on Kafka or on SQS by changing one registration. Publishers and handlers never know which.</td></tr>
<tr><td><b>Background services that do not drift</b></td><td>Scheduled from the clock, not from when the last run finished, with a distributed lock so one instance runs each occurrence.</td></tr>
<tr><td><b>Tests that move time</b></td><td>.NET's <code>TimeProvider</code> throughout: "a month later the fee applies once" is a test that runs in milliseconds, background services included.</td></tr>
<tr><td><b>Repositories with filters</b></td><td>Entity, edit and filter records; one <code>Get(filter)</code> instead of a method per question; pagination, soft delete and search as opt-ins, on EF Core and PostgreSQL.</td></tr>
<tr><td><b>Errors that mean something</b></td><td>The domain throws not-found, conflict or invalid-operation; the web layer answers 404, 409 or 422 as problem+json with the request id — never a stack trace.</td></tr>
<tr><td><b>Scaffolding that keeps its shape</b></td><td><code>mcs</code> creates services, domains and entities, and <code>mcs validate</code> keeps a service reading the same at a hundred use cases as at ten.</td></tr>
</table>

## Quick start

Needs the .NET 10 SDK, and PostgreSQL for a service with a database.

```bash
dotnet tool install -g MagicCSharp.Cli

mcs init --prefix Acme                                    # a repository, with build rules and AGENTS.md
mcs create-app --name Shop --database shop                # a service
mcs create-domain --solution Shop --name Orders --models --tests
mcs add-entity --solution Shop --domain Orders --name Order --paginated
dotnet run --project Apps/Shop/Shop.App
```

Or add the packages to an app you already have — the layout and `mcs` are optional, and every package stands
alone:

```bash
dotnet add package MagicCSharp.App
```

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddMagicApp();

var app = builder.Build();
app.UseMagicApp(builder);
app.Run();
```

That registers use cases, `TimeProvider`, Snowflake ids, request IDs, in-process events, scheduling defaults and
problem-details error handling, then resolves every registration once at startup so a miswired dependency
fails the deploy rather than the first request. Every call it makes is public on the package that owns it, so
outgrowing the defaults means replacing two lines with five. [What each option does →](src/MagicCSharp.App/)

The [example](examples/PropertyManagement/) is a whole service built this way: signing a lease as a chain of use
cases, late fees from a background service, notifications over Kafka, and tests that move the clock.

## What a use case looks like

```csharp
public record PlaceOrderRequest
{
    public required long CustomerId { get; init; }
    public required decimal Total { get; init; }
}

public interface IPlaceOrderUseCase : IMagicUseCase
{
    Task<Order> Execute(PlaceOrderRequest request);
}

public class PlaceOrderUseCase(
    IOrdersRepository ordersRepository,
    IEventDispatcher eventDispatcher) : IPlaceOrderUseCase
{
    public async Task<Order> Execute(PlaceOrderRequest request)
    {
        var order = await ordersRepository.Create(new OrderEdit
        {
            CustomerId = request.CustomerId,
            Total = request.Total,
            Status = OrderStatus.Pending,
        });

        eventDispatcher.Dispatch(new OrderPlacedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
        });

        return order;
    }
}
```

The test constructs it. No host, no database, no mocking framework — `FakeOrdersRepository` and
`RecordingEventDispatcher` are a few lines you write against the two interfaces the use case asked for:

```csharp
var placeOrder = new PlaceOrderUseCase(new FakeOrdersRepository(), new RecordingEventDispatcher());

var order = await placeOrder.Execute(new PlaceOrderRequest { CustomerId = 7, Total = 42.50m });

Assert.Equal(OrderStatus.Pending, order.Status);
```

```csharp
builder.Services.AddMagicCSharp();
```

That one call finds every `IMagicUseCase` and registers it under its own interface. A controller takes
`IPlaceOrderUseCase`; there is no wiring to write or to forget. The next operation is another class, not
another method on a god object.

## Big work is a handful of small operations

A checkout is not `CheckoutService.DoEverything`:

```csharp
public class CheckoutUseCase(
    IPlaceOrderUseCase placeOrder,
    IAttachPaymentUseCase attachPayment,
    IEventDispatcher eventDispatcher) : ICheckoutUseCase
{
    public async Task<CheckoutResult> Execute(CheckoutRequest request)
    {
        var order = await placeOrder.Execute(new PlaceOrderRequest
        {
            CustomerId = request.CustomerId,
            Total = request.Total,
        });

        await attachPayment.Execute(new AttachPaymentRequest
        {
            OrderId = order.Id,
            PaymentMethodId = request.PaymentMethodId,
        });

        eventDispatcher.Dispatch(new CheckoutCompletedEvent { OrderId = order.Id });

        return new CheckoutResult { OrderId = order.Id };
    }
}
```

Must-happen stays in the chain (`placeOrder`, `attachPayment`). Should-happen-later — the confirmation email,
the search index, the metadata — is a handler on the event. You can read a checkout in one screen. You can
test each step without the others.

The chain is two writes, not one transaction. If `attachPayment` throws, the order from `placeOrder` is
already persisted — as `Pending`, which is the state an order without a payment should be in, and why that
status exists. A step that must not stand without the next one belongs inside the same use case, not after
it.

The packages exist so this style does not collapse the first time you need a clock, a job, a filter or a 404.

## Small operations are what an AI agent needs

Ask an agent to change how late fees work in a codebase built around a `LeaseService`, and it has to read two
thousand lines to find the part that matters, then edit a class that forty other things call. Whatever it gets
wrong, it gets wrong somewhere shared.

Ask the same thing here and the unit of work is `ApplyLateFeesUseCase`: one file, one `Execute`, one test class.

- **The scope is a class.** What an operation can touch is its constructor. The agent reads the use case, the
  interfaces it takes and its tests — a few hundred lines, not the service — so it spends its context on the
  problem instead of on finding it.
- **The blast radius is visible.** A use case reaches only what its constructor names, and the only code a
  change to it can affect is what takes its interface — one search finds every caller. The diff is one class and
  its test: a review a person can actually do, which matters more the more code an agent writes.
- **The check is fast.** A use case is constructed in a test with fakes and a `FakeTimeProvider`, so "a month
  later the fee applies once" is a test that runs in milliseconds. The agent verifies its own change instead of
  asking you to.
- **The conventions are compile errors.** `MagicCSharp.Analyzers` turns the house style into build errors —
  `DateTime.Now`, a positional record, a variable called `useCase`, a missing brace — so an agent that drifts
  is corrected by the compiler, on the spot, not by a reviewer three days later.
- **The structure comes from a tool.** `mcs` creates services, domains and entities, so an agent adds an entity
  the way everyone else does instead of inventing a folder layout.
- **The rules are written down for it.** `mcs init` writes an `AGENTS.md` and an `.ai-knowledge/` folder — where
  a use case goes, how an entity is shaped, how events, background services and tests work — so the agent
  follows the same conventions the build enforces. `mcs update ai-files` brings in improved guides with each
  release.

None of this makes an agent right. It makes it wrong in small, visible, testable places — which is the
difference between an agent you supervise line by line and one you can hand a task to.

## Where to go next

| You want to | Start here |
|---|---|
| See a whole service | [The property-management example](examples/PropertyManagement/) |
| Write use cases and wire a host | [MagicCSharp](src/MagicCSharp/) · [MagicCSharp.App](src/MagicCSharp.App/) |
| Store entities | [MagicCSharp.Data](src/MagicCSharp.Data/) · [.Data.EntityFramework](src/MagicCSharp.Data.EntityFramework/) · [.Data.Postgres](src/MagicCSharp.Data.Postgres/) |
| Publish and handle events | [MagicCSharp.Events](src/MagicCSharp.Events/) · [.Events.Kafka](src/MagicCSharp.Events.Kafka/) · [.Events.SQS](src/MagicCSharp.Events.SQS/) |
| Run work on a schedule | [MagicCSharp.Scheduling](src/MagicCSharp.Scheduling/) |
| Test with fakes, time and a real database | [MagicCSharp.Testing](src/MagicCSharp.Testing/) · [.Testing.Database](src/MagicCSharp.Testing.Database/) |
| Enforce the house style | [MagicCSharp.Analyzers](src/MagicCSharp.Analyzers/) |
| Scaffold and lay out a repository | [mcs](src/MagicCSharp.Cli/) · [The layout guide](docs/repository-layout.md) · [Template overrides](docs/template-overrides.md) |
| Give an AI agent the conventions | [AIAgents/](AIAgents/) — what `mcs init` writes as `AGENTS.md` and `.ai-knowledge/` |
| Upgrade | [CHANGELOG](CHANGELOG.md), with an old → new table for every breaking release |

The rest of this page goes deeper, one part at a time.

## Use cases

A use case is one business operation, as a plain class. No base class, no framework types in the signature,
and nothing to register by hand. The interface is the point: it is what a controller depends on, what a test
replaces, and what stops the next caller reaching past the operation into the repository underneath.

Two implementations of one interface is a startup error naming both, rather than whichever reflection
returned first. An interface extending the marker with no implementation is also a startup error, rather
than a resolution failure on the first request that needs it. `[MagicUseCase(ServiceLifetime.Singleton)]`
changes the lifetime when the default scoped is wrong.

A use case that needs another takes its interface, exactly as `CheckoutUseCase` does above. `Lazy<IXxxUseCase>`
is registered alongside every interface, for the rare pair that call each other conditionally.

### Which makes them testable without a host

The dependencies are interfaces, so a test constructs the thing directly — the `new PlaceOrderUseCase(...)`
above is the whole setup. The fakes are yours, in the test project, and there is no framework double to
learn.

`MagicCSharp.Testing` supplies doubles for the framework's own seams — most usefully `FakeTimeProvider`, .NET's
own test clock, which you move by hand, so anything time-dependent is testable in milliseconds rather than by
waiting:

```csharp
timeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
await createLease.Execute(request);

timeProvider.Advance(TimeSpan.FromDays(31));
await applyLateFees.Execute();          // now asserts against a month later
```

Code that reads `DateTime.Now` cannot do this, which is why the analyzers make it a build error.

## Repositories

An entity is three records that agree: what you can write, what comes back, and what you can query by.

```csharp
public record Order : OrderEdit, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record OrderEdit                    // Order derives from it, so an entity
{                                          // is accepted wherever an edit is
    public required long CustomerId { get; init; }
    public required decimal Total { get; init; }
    public required OrderStatus Status { get; init; }
}

public class OrderFilter                   // a null property means "do not narrow on this"
{
    public long? CustomerId { get; init; }
    public OrderStatus? Status { get; init; }
    public ComparableRange<DateTimeOffset>? Created { get; init; }
}
```

The interface your domain depends on says what the entity supports. Pagination, soft delete and a search
column are separate opt-ins, so a caller can see from the interface which of them exist:

```csharp
public interface IOrdersRepository :
    IRepository<Order, long, OrderEdit, OrderFilter>,
    IPaginatedRepository<Order, OrderFilter>;
```

That is everything the domain sees. The implementation lives behind it and inherits all of it except the two
things only you know — how to build a row, and what the filter means:

```csharp
public class OrdersEfRepository(
    IDbContextFactory<MagicShopContext> contextFactory,
    IKeyGenService keyGenService,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseIdPaginatedRepository<MagicShopContext, OrderDal, Order, OrderFilter, OrderEdit>(
        contextFactory, timeProvider, loggerFactory),
      IOrdersRepository
{
    protected override OrderDal CreateDal(OrderEdit edit)
    {
        return OrderDal.From(edit, keyGenService.GetId());
    }

    protected override IQueryable<OrderDal> ApplyFilter(IQueryable<OrderDal> query, OrderFilter filter)
    {
        query = query.ApplyNullableValueFilter(filter.CustomerId, x => (long?)x.CustomerId);
        query = query.ApplyNullableValueFilter(filter.Status, x => (OrderStatus?)x.Status);
        query = query.ApplyComparableRangeFilter(filter.Created, x => (DateTimeOffset?)x.Created);

        return query;
    }
}
```

You get create, read by key or filter, four flavours of update, delete, counts and pages — each opening its
own context, each translating to SQL. `GetOrThrow` raises the domain's `NotFoundException` so an endpoint
that fetches by id needs no null branch.

`OrderDal` is the row: the `[Table]` and `[Column]` attributes, the EF types, the mapping to and from
`Order`. It lives in the data project, and `Order` does not know it exists — the arrow points from storage
toward the domain and never back. The contracts package has no Entity Framework dependency at all, so a
domain project referencing it gets the interfaces and no persistence library.

The paved path is EF + Postgres. The port is not. DynamoDB for writes and Elasticsearch for queries can sit
behind the same `IOrdersRepository`; the filter is where you admit what that store can actually query, and
adding a property to it is the moment to know which adapter you are stretching.

Timestamps are written UTC. Enums are stored by name, so inserting a member into the middle of one does not
change what existing rows mean.

## Events

An event is a record carrying ids and primitives — never an entity, because it will be deserialized by code
built from a different commit than the one that published it:

```csharp
public record OrderPlacedEvent : MagicEvent
{
    public required long OrderId { get; init; }
    public required long CustomerId { get; init; }
}
```

Anything that depends on `IEventDispatcher` can publish one, and handlers are discovered the same way use
cases are — implement the interface and it runs:

```csharp
public class SendConfirmationHandler(IEmailService emailService) : IEventHandler<OrderPlacedEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public async Task Handle(OrderPlacedEvent orderPlacedEvent)
    {
        await emailService.ConfirmOrder(orderPlacedEvent.OrderId);
    }
}

public class UpdateSearchIndexHandler(ISearchIndex searchIndex) : IEventHandler<OrderPlacedEvent>
{
    public async Task Handle(OrderPlacedEvent orderPlacedEvent)
    {
        await searchIndex.IndexOrder(orderPlacedEvent.OrderId);
    }
}
```

Several handlers for one event run independently, ordered by `Priority` — which is `static`, because the
registration reads it without constructing the handler. Adding a capability is adding a class; nothing that
publishes the event changes.

The transport is one registration, and it is the only line that differs between running locally and running
on a queue:

```csharp
services.AddLocalMagicEvents();               // in-process
services.AddMagicKafkaEvents(kafkaConfig);    // Kafka
services.AddMagicSqsEvents(sqsConfig);        // SQS
```

Publishers and handlers do not change when that line does. Publishing to two transports at once is not a
registration the framework ships; it is a composite `IEventDispatcher` you write, a few lines against the
same interface — and publishers and handlers still do not change.

**Dispatch is fire-and-forget on all three, deliberately.** In-process handlers run on a background task
rather than blocking the caller, because that is what Kafka and SQS do, and a handler that only works when
dispatch blocks would break on the day you switch.

An event means "this happened — carry on". Anything that *must* happen belongs in the use case. Handlers are
the side processes — email, metadata, search, the next workflow — and they are eventually consistent on
purpose, so each one should be safe to run twice. Retries, dead letters and delivery belong to whoever
builds the transport adapter, not to the code that raised the event. The one operation that genuinely needs
a particular bus can take that bus directly; the default path stays on `IEventDispatcher`. Each transport's
README states exactly what it does and does not guarantee.

## Time, ids and jobs

`TimeProvider` — .NET's own clock abstraction — instead of `DateTime.Now`, so a test can move time rather than
wait for it. `IKeyGenService` gives
Snowflake ids — 64-bit, time-sortable, assigned before the insert, so a caller knows an entity's id without
a round trip and two instances never collide without coordinating. For keys that appear in a URL there are
unguessable string keys instead.

Background jobs schedule from the clock rather than from when the last run finished, so an hourly job stays
on the hour instead of drifting by however long each run took:

```csharp
public class ExpireHoldsBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<ExpireHoldsBackgroundService> logger)
    : ScheduledBackgroundService(
        serviceScopeFactory, new IntervalSchedule(TimeSpan.FromHours(1)), null, timeProvider, logger)
{
    protected override string ScheduleKey => "expire-holds";
    protected override string ServiceName => nameof(ExpireHoldsBackgroundService);

    protected override async Task ExecuteScheduledTask(CancellationToken stoppingToken)
    {
        // A scope per run, because the job outlives any one of them.
        await using var scope = ServiceScopeFactory.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IExpireHoldsUseCase>().Execute();
    }
}
```

The job calls a use case. The schedule is not a place to hide business logic.

`TimeOfDaySchedule` is the other one, and takes a timezone, so "2am local" survives a daylight-saving
change. Every instance wakes on the same boundary and takes a distributed lock named for the job, so only
one runs it — the defaults suit a single machine, and running several means registering a lock provider and
a schedule store that they share.

## Errors

A use case throws what it means. `NotFoundException` from the domain becomes a 404 with a problem+json body;
`EntityConflictException` a 409 and `EntityInvalidOperationException` — the entity's state refuses it, like
paying a charge twice — a 422; a `ValidationException` or a bad argument becomes a 400; anything unexpected becomes a 500 whose detail is
logged rather than returned, because an unhandled exception's message routinely contains a connection string
or a row nobody should see. `HttpException` and its subclasses are there for the times a use case genuinely
means a status code.

```csharp
public class GetOrderUseCase(IOrdersRepository ordersRepository) : IGetOrderUseCase
{
    public Task<Order> Execute(long orderId)
    {
        return ordersRepository.GetOrThrow(orderId);   // 404 if it is not there
    }
}
```

The body carries the same request id as the `X-Request-ID` header, so an id quoted in a bug report finds the
request in the logs.

## The packages

Fourteen, split so you take only what you use. `MagicCSharp.App` bundles the four a web service needs, for
when you would rather not choose.

| Package | Add it when you want | Brings with it |
|---|---|---|
| **[MagicCSharp.App](src/MagicCSharp.App/)** | A web service wired in two calls | the four below it |
| **[MagicCSharp](src/MagicCSharp/)** | Use cases, Snowflake ids, request IDs, `Optional<T>`, `TimeProvider` registration | DI + logging abstractions, IdGen |
| **[MagicCSharp.AspNetCore](src/MagicCSharp.AspNetCore/)** | Request-ID middleware, RFC 7807 error handling, startup preflight | the ASP.NET shared framework |
| **[MagicCSharp.Scheduling](src/MagicCSharp.Scheduling/)** | Drift-free background jobs | DistributedLock, hosting |
| **[MagicCSharp.Data](src/MagicCSharp.Data/)** | Repository contracts, pagination, LINQ filter helpers | nothing — no persistence library |
| **[MagicCSharp.Data.EntityFramework](src/MagicCSharp.Data.EntityFramework/)** | The repository base classes and DALs | EF Core |
| **[MagicCSharp.Data.Postgres](src/MagicCSharp.Data.Postgres/)** | Pooled context factory, design-time factory, UTC interceptor | Npgsql |
| **[MagicCSharp.Events](src/MagicCSharp.Events/)** | Event dispatch and handler discovery | System.Text.Json |
| **[MagicCSharp.Events.Kafka](src/MagicCSharp.Events.Kafka/)** | Kafka transport | Confluent.Kafka |
| **[MagicCSharp.Events.SQS](src/MagicCSharp.Events.SQS/)** | SQS transport | AWSSDK.SQS |
| **[MagicCSharp.Testing](src/MagicCSharp.Testing/)** | `FakeTimeProvider`, `FakeKeyGen`, `SyncEventDispatcher`, in-memory locks | DistributedLock, Microsoft's TimeProvider.Testing |
| **[MagicCSharp.Testing.Database](src/MagicCSharp.Testing.Database/)** | Repository tests against real PostgreSQL | Testcontainers, xUnit |
| **[MagicCSharp.Analyzers](src/MagicCSharp.Analyzers/)** | The code-style conventions as compile errors | nothing — a development dependency, added by `mcs init` |
| **[MagicCSharp.Cli](src/MagicCSharp.Cli/)** | The `mcs` tool | installed globally, not referenced |

A domain project referencing `MagicCSharp.Data` gets the repository interfaces and no persistence library at
all — which is the point of the split. Wanting `FakeTimeProvider` does not mean wanting Docker.

## An optional layout

Everything above works in any project, arranged however you like. The arrangement it was designed for is
**domain services in one repository**: several services, each deployed on its own and each owning a whole
business domain — its use cases, its data, its endpoints and its background work — sharing libraries, tooling
and conventions.

Domain services sit between a monolith and microservices. There are far fewer moving parts than a fleet of
microservices, and one repository to change them in, yet each service still deploys, scales and fails on its
own. Services talk to each other through events, never through each other's databases. If you do want
microservices, make the services smaller; nothing about the layout changes.

Inside a service, the domain grows as a tree, each part owning its use cases, entities, endpoints and tests:

```
Apps/
  Shop/                          a service, deployed on its own
    Shop.App/                    Program.cs — a list of references and little else
    Shop.Domains/Orders/
      Default/                   use cases, event handlers
      Models/                    entities, edits, filters
      App/                       this domain's controllers
      Tests/
      Fulfilment/                a subdomain: the same shape, one level down
    Data/
      Data.Models/               repository interfaces — no EF dependency
      Data.EntityFramework/      DALs, repositories, context, migrations
  Notifications/                 another service, with its own domains and database
Libs/
  Events/                        the event contracts the services share
```

A domain grows by gaining siblings rather than getting wider, and each brings its own endpoints, so
`Shop.App` never becomes the folder where every feature's controllers pile up. Where a class goes and what
it may reference are decided by the layout, enforced by the build and by `mcs validate` in CI, and generated
by a tool, so a service reads the same at a hundred use cases as it did at ten.

`mcs` is a dotnet global tool that creates and maintains it:

```bash
dotnet tool install -g MagicCSharp.Cli

mcs init --prefix Acme
mcs create-app --name Shop --database shop
mcs create-domain --solution Shop --name Orders --models --tests
mcs create-domain --solution Shop --name Orders.App
mcs add-entity --solution Shop --domain Orders --name Order --paginated
```

`add-entity` writes the four files an entity needs, across three projects that each have to agree about
names, namespaces and generic arguments, and registers it — not typing saved so much as a class of mistake
removed. Nothing is ever overwritten, and re-running any command produces no diff.

`mcs init` also writes the conventions down for AI coding agents: an `AGENTS.md` — which Claude Code, Codex,
Cursor and the rest all read, `CLAUDE.md` importing it — and an `.ai-knowledge/`
folder, one guide per topic — where a use case goes, how an entity is shaped, how events, background services
and tests work — plus a `project.md` for what is specific to your repository. An agent working in the
repository follows the same rules the build enforces, and `mcs update ai-files` brings in improved guides with
each release. Their source is [AIAgents/](AIAgents/).

**[The full guide →](docs/repository-layout.md)** — domains, subdomains, app libraries, entities, and what
the tool wires versus what it leaves you. **[Template overrides →](docs/template-overrides.md)** — every
file it generates comes from a template you can replace, one at a time, keeping the rest.

## Requirements

The .NET 10 SDK. The libraries target net9.0; the CLI, the tests and generated repositories target net10.0.
PostgreSQL for the data packages. Docker only for `MagicCSharp.Testing.Database`.

## Where it comes from

This is how MagicDoor, Revoco and AgentParley write their C# services: the same use-case shape, the same
ports, the same tests. It was extracted from MagicDoor's backend so the next service starts from the same
defaults rather than a folder copied from the last one.

The engineers who designed it came from Amazon and Google. The rule they kept: an operation is a class you
can hold in your head, and infrastructure stays behind an interface. If you cannot test it with
`new PlaceOrderUseCase(...)`, the operation is too big.

The opinions are not theoretical. They are what was left after finding out which pieces survive a codebase
getting large and a team changing. Where a decision has a cost, the cost is written next to it. Judge them
on that.

## Contributing

The framework is small on purpose — issues, template changes and new `validate` rules are all welcome.
[CONTRIBUTING.md](CONTRIBUTING.md) covers building it, running the tests without Docker, and what to raise
before writing code. [GOVERNANCE.md](GOVERNANCE.md) says who maintains it and how breaking changes are
versioned. To report a vulnerability see [SECURITY.md](SECURITY.md) — please do not open a public issue.

## License

MIT. See [LICENSE](LICENSE).
