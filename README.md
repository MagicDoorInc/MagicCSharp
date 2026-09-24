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

**Business logic as small use cases: a C# framework for developers and their coding agents.**

One operation is one class with one `Execute`. Whether you change an operation or Claude Code, Codex, Cursor
or Copilot does, the diff is usually that class and its test, small enough to review. `mcs init` writes the conventions into `AGENTS.md`
for the agent, and about twenty Roslyn rules make them build errors. Every C# service at
[MagicDoor](https://magicdoor.com), [Revoco](https://revoco.ai) and [AgentParley](https://agentparley.ai)
runs on MagicCSharp, an MIT-licensed framework designed by engineers from Amazon and Google.

[Quick start](#quick-start) · [Working with coding agents](#working-with-coding-agents) · [Example service](examples/PropertyManagement/) · [Packages](#the-packages) · [Layout guide](docs/repository-layout.md) · [Changelog](CHANGELOG.md)

<table>
<tr><td><b>Guides for the agent</b></td><td><code>mcs init</code> writes <code>AGENTS.md</code> for Codex, Cursor, Copilot and most agents, <code>CLAUDE.md</code> for Claude Code (it imports <code>AGENTS.md</code>), and one guide per topic in <code>.ai-knowledge/</code>.</td></tr>
<tr><td><b>One operation, one class</b></td><td>A business operation is a class with one <code>Execute</code>, and <code>AddMagicCSharp()</code> finds and registers every one. Bigger work is those classes called in order.</td></tr>
<tr><td><b>House style as build errors</b></td><td><code>MagicCSharp.Analyzers</code> ships about twenty Roslyn rules. <code>DateTime.Now</code>, a positional record, a missing brace or a dependency called <code>useCase</code> fails the build.</td></tr>
<tr><td><b>Tests that move time</b></td><td>.NET's <code>TimeProvider</code> throughout, background services included. Tests run the real use cases against a real PostgreSQL, with a <code>FakeTimeProvider</code> the test moves.</td></tr>
<tr><td><b>Events on any transport</b></td><td>Publish through one <code>IEventDispatcher</code>; handlers are discovered. In-process, Kafka or SQS is one registration, and publishers and handlers are the same on all three.</td></tr>
<tr><td><b>Drift-free background services</b></td><td>Interval or time-of-day schedules from the clock, so an hourly job stays on the hour. One instance runs each occurrence under a lock named for the service, file-based unless you register a shared provider.</td></tr>
<tr><td><b>Repositories with filters</b></td><td>Entity, edit and filter records; one <code>Get(filter)</code> per entity; pagination, soft delete and search as opt-ins, on EF Core and PostgreSQL. Snowflake ids are assigned before the insert.</td></tr>
<tr><td><b>Errors that mean something</b></td><td>The domain throws not-found, conflict, invalid-operation or validation; the web layer answers 404, 409, 422 or 400 as problem+json with the request id. A 500's detail is logged and hidden outside Development.</td></tr>
<tr><td><b>Structure from a tool</b></td><td><code>mcs</code> creates services, domains and entities the same way every time, and re-running any command changes nothing. <code>mcs validate</code> checks the handful of conventions the compiler cannot.</td></tr>
<tr><td><b>Domain services, one repository</b></td><td>Each app under <code>Apps/</code> is its own deployable service for one business area, split inside into domains. Apps share <code>Libs/</code> and talk through events. Make them smaller and you have microservices.</td></tr>
</table>

## Quick start

Needs the .NET 10 SDK, PostgreSQL for a service with a database, and Docker for the repository tests. The
generated settings expect PostgreSQL on localhost as `postgres`/`postgres`.

```bash
dotnet tool install -g MagicCSharp.Cli

mcs init --prefix Acme                                        # build rules, AGENTS.md, CLAUDE.md, .ai-knowledge/
mcs create-app --name Leasing --database leasing              # a service
mcs create-domain --solution Leasing --name Leases --models --tests
mcs add-entity --solution Leasing --domain Leases --name Lease --paginated
dotnet run --project Apps/Leasing/Leasing.App
```

`add-entity` wrote the entity, edit and filter records, the DAL, the repository interface and its EF
implementation, the `DbSet` and the registration. `mcs init` wrote the build rules and the conventions for
the agent:

```
AGENTS.md                                  the guide Codex, Cursor, Copilot and most agents read
CLAUDE.md                                  @AGENTS.md, so Claude Code reads the same text
.ai-knowledge/
  INDEX.md                                 which guide to read for which task
  architecture-and-project-structure.md    where code goes
  use-case-patterns.md
  entities-and-database.md
  events-and-messaging.md
  background-services.md
  api-and-controller-conventions.md
  time-and-dates.md
  testing-conventions.md
  coding-style.md
  project-tooling.md
  project.md                               what is specific to this repository; yours, and mcs leaves it alone
```

Now open the repository in Claude Code, Codex, Cursor or Copilot. The agent reads `AGENTS.md` at the root.
It says that business logic goes in use cases, that domain code depends on ports, and that `mcs` creates
projects and entities. A change is done when `dotnet build`, `dotnet test` and `mcs validate` pass.

Or add the packages to an app you already have. There are fourteen, under one version number, and each
stands alone; the layout and `mcs` are optional.

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

Those two calls register use cases, `TimeProvider`, Snowflake ids, request ids, in-process events,
scheduling defaults and problem-details errors. At startup they resolve every registration once, so a
miswired dependency fails the deploy. [What each option does →](src/MagicCSharp.App/)

The [example](examples/PropertyManagement/) is a whole leasing service built from the published packages.
It signs a lease as a chain of use cases, applies late fees from an hourly background service and sends
notifications over Kafka. Its tests move the clock, and its CI builds it from nuget.org every week.

## What a use case looks like

A request record, an interface and a class. The interface extends `IMagicUseCase`, and that is the only
framework type in it. This is `CreateLeaseUseCase` from the example, without its logging and argument checks:

```csharp
public record CreateLeaseRequest
{
    public required long PropertyId { get; init; }
    public required string TenantName { get; init; }
    public required string TenantEmail { get; init; }
    public required decimal MonthlyRent { get; init; }
    public required decimal SecurityDeposit { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
}

public interface ICreateLeaseUseCase : IMagicUseCase
{
    Task<Lease> Execute(CreateLeaseRequest request);
}

public class CreateLeaseUseCase(ILeasesRepository leasesRepository) : ICreateLeaseUseCase
{
    public async Task<Lease> Execute(CreateLeaseRequest request)
    {
        if (request.EndDate <= request.StartDate)
        {
            throw new ValidationException("A lease must end after it starts.");
        }

        return await leasesRepository.Create(new LeaseEdit
        {
            PropertyId = request.PropertyId,
            TenantName = request.TenantName,
            TenantEmail = request.TenantEmail,
            MonthlyRent = request.MonthlyRent,
            SecurityDeposit = request.SecurityDeposit,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        });
    }
}
```

`builder.Services.AddMagicCSharp()` finds every `IMagicUseCase` and registers it under its interface, so a
controller takes `ICreateLeaseUseCase` and nothing is wired by hand. A test constructs the class directly;
`FakeLeasesRepository` is a class in the test project that implements `ILeasesRepository`:

```csharp
var createLease = new CreateLeaseUseCase(new FakeLeasesRepository());

var creating = createLease.Execute(new CreateLeaseRequest
{
    PropertyId = 1,
    TenantName = "Dana Whitfield",
    TenantEmail = "dana@example.com",
    MonthlyRent = 1850m,
    SecurityDeposit = 1850m,
    StartDate = new DateOnly(2027, 3, 1),
    EndDate = new DateOnly(2026, 3, 1),
});

await Assert.ThrowsAsync<ValidationException>(() => creating);
```

## Bigger work is a handful of small operations

Signing a lease is three operations called in order, then an event. Each operation exists on its own. This
is `SignLeaseUseCase` from the example, without its logging and the deposit charge:

```csharp
public class SignLeaseUseCase(
    IGetPropertiesUseCase getProperties,
    ICreateLeaseUseCase createLease,
    ICreateChargesUseCase createCharges,
    IEventDispatcher eventDispatcher) : ISignLeaseUseCase
{
    public async Task<SignLeaseResult> Execute(SignLeaseRequest request)
    {
        var property = await getProperties.Execute(request.PropertyId);
        NotFoundException.ThrowIfNull(property, request.PropertyId);

        var lease = await createLease.Execute(new CreateLeaseRequest
        {
            PropertyId = property.Id,
            TenantName = request.TenantName,
            TenantEmail = request.TenantEmail,
            MonthlyRent = request.MonthlyRent,
            SecurityDeposit = request.SecurityDeposit,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        });

        var charges = await createCharges.Execute(
        [
            new ChargeEdit
            {
                LeaseId = lease.Id,
                Type = ChargeType.Rent,
                Amount = lease.MonthlyRent,
                DueDate = lease.StartDate,
            },
        ]);

        eventDispatcher.Dispatch(new LeaseSignedEvent
        {
            LeaseId = lease.Id,
            PropertyId = property.Id,
        });

        return new SignLeaseResult
        {
            Lease = lease,
            Charges = charges,
        };
    }
}
```

The steps that must happen are in the chain: `getProperties`, `createLease`, `createCharges`. The welcome
email is a handler on `LeaseSignedEvent`, because a lease is signed whether or not the email goes. A second
reaction to signing is another handler class, and this class stays as it is.

A chain is not a transaction. If `createCharges` throws, the lease that `createLease` wrote is already
there, and the caller gets the exception and decides. Two steps that must succeed together go inside one
use case.

## Working with coding agents

Ask an agent to change late fees in a service built around a `LeaseService`, and it edits a class that many
other things call. The reviewer reads all of it. Ask the same thing here and the unit of work is
`ApplyLateFeesUseCase`: one file, one `Execute`, one test class. The agent can still be wrong. What changes
is where a mistake can land and how it is found:

- **The scope is a class.** What an operation can touch is listed in its constructor. The agent reads the
  use case, the interfaces it takes and its test class.
- **Its callers are one search away.** They are the classes that take `IApplyLateFeesUseCase`. The diff is
  the class and its test, small enough for a person to read all of it.
- **The check is real.** One `dotnet test` runs the real use cases, repositories and handlers against
  PostgreSQL in a throwaway container, with a `FakeTimeProvider` the test moves. "Three days later the fee
  is still charged once" advances the clock three days and runs the use case again.
- **The conventions are compile errors.** `MagicCSharp.Analyzers` turns the house style into build errors,
  and the message says what to do. This repository builds under the same rules.

  ```
  error MCS0008: Replace 'DateTime.UtcNow' with an injected TimeProvider and call 'timeProvider.GetUtcNow()'
  ```

- **The structure comes from `mcs`.** `AGENTS.md` tells the agent to use `mcs` for every project, entity
  and repository, so it adds an entity the way everyone else does.
- **The rules are written down for it.** `AGENTS.md` is about fifty lines and sends the agent to
  `.ai-knowledge/INDEX.md`, which names the guide each task needs. It also tells the agent to ask before
  widening the scope, to commit only when asked, and to say which checks it ran.
  `mcs update ai-files` refreshes the shipped guides to the installed `mcs` version and leaves `project.md`
  alone. Their source is [AIAgents/](AIAgents/).

The same things make a person's pull request reviewable.

## Where to go next

| You want to | Start here |
|---|---|
| See a whole service | [The property-management example](examples/PropertyManagement/) |
| Give an AI agent the conventions | [AIAgents/](AIAgents/), the source of `AGENTS.md`, `CLAUDE.md` and `.ai-knowledge/` |
| Write use cases and wire a host | [MagicCSharp](src/MagicCSharp/) · [MagicCSharp.App](src/MagicCSharp.App/) |
| Store entities | [MagicCSharp.Data](src/MagicCSharp.Data/) · [.Data.EntityFramework](src/MagicCSharp.Data.EntityFramework/) · [.Data.Postgres](src/MagicCSharp.Data.Postgres/) |
| Publish and handle events | [MagicCSharp.Events](src/MagicCSharp.Events/) · [.Events.Kafka](src/MagicCSharp.Events.Kafka/) · [.Events.SQS](src/MagicCSharp.Events.SQS/) |
| Run work on a schedule | [MagicCSharp.Scheduling](src/MagicCSharp.Scheduling/) |
| Test with fakes, time and a real database | [MagicCSharp.Testing](src/MagicCSharp.Testing/) · [.Testing.Database](src/MagicCSharp.Testing.Database/) |
| Enforce the house style | [MagicCSharp.Analyzers](src/MagicCSharp.Analyzers/) |
| Scaffold and lay out a repository | [mcs](src/MagicCSharp.Cli/) · [The layout guide](docs/repository-layout.md) · [Template overrides](docs/template-overrides.md) |
| Deploy only the apps a change touched | [The CI/CD guide](docs/ci-cd.md) · `mcs affected` |
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

A use case that needs another takes its interface, exactly as `SignLeaseUseCase` does above. `Lazy<IXxxUseCase>`
is registered alongside every interface, for the rare pair that call each other conditionally.

### Which makes them testable without a host

The dependencies are interfaces, so a test constructs the thing directly — the `new CreateLeaseUseCase(...)`
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
| **[MagicCSharp](src/MagicCSharp/)** | Use cases, Snowflake ids, request IDs, `TimeProvider` registration | DI + logging abstractions, IdGen |
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
**domain services in one repository**, built from two kinds of thing:

| | What it is | At MagicDoor |
|---|---|---|
| **App** | A deployable service, with its own executable. It owns one business area: its data, endpoints and background work. | Maintenance, Auth, Accounting |
| **Domain** | A part of an app. An app's domains deploy together, in the app's one executable, but are kept apart inside it: each has its own use cases, entities, endpoints and tests. Domains of the same app may call each other through their use cases. | Vendors, MaintenanceRequests and VendorScheduling, inside Maintenance |

The apps share libraries, tooling and conventions, but not their data.

Domain services sit between a monolith and microservices. There are far fewer moving parts than a fleet of
microservices, and one repository to change them in, yet each service still deploys, scales and fails on its
own. Apps talk to each other through events, never through each other's databases. If you do want
microservices, make the apps smaller; nothing about the layout changes.

Inside an app, the domains grow as a tree, each owning its use cases, entities, endpoints and tests:

```
Apps/
  Shop/                          an app: one deployable service
    Shop.App/                    Program.cs — a list of references and little else
    Shop.Domains/Orders/         a domain inside it
      Default/                   use cases, event handlers
      Models/                    entities, edits, filters
      App/                       this domain's controllers
      Tests/
      Fulfilment/                a subdomain: the same shape, one level down
    Data/
      Data.Models/               repository interfaces — no EF dependency
      Data.EntityFramework/      DALs, repositories, context, migrations
  Notifications/                 a second app: its own executable, domains and database
    Notifications.App/
    Notifications.Domains/
      Recipients/                who to write to, kept from the events Shop publishes
      Emails/                    a second domain; may call Recipients' use cases
    Data/
      Data.Models/
      Data.EntityFramework/
Libs/
  Events/                        the event contracts the apps share: Shop publishes, Notifications handles
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
`new CreateLeaseUseCase(...)`, the operation is too big.

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
