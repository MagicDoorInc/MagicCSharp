# MagicCSharp

A set of C# packages for the parts of an application that are not your business logic: use cases that
register themselves, repositories that turn a filter into SQL, events with three transports behind one
interface, a clock you can move in a test, sortable ids, background jobs that do not drift, and errors that
reach the caller as RFC 7807 problems rather than stack traces.

Every package stands alone. Add one to a project you already have and nothing else about it changes — the
core has three dependencies and knows nothing about ASP.NET, Entity Framework or Kafka.

There is also an optional repository layout and a tool that generates it, for people who want the whole
arrangement these were designed for. That is the last section, and you can ignore it entirely.

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
public class OrdersEfRepository(...)
    : BaseIdPaginatedRepository<MagicShopContext, OrderDal, Order, OrderFilter, OrderEdit>(...),
      IOrdersRepository
{
    protected override OrderDal CreateDal(OrderEdit edit) => OrderDal.From(edit, keyGen.GetId());

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
public class SendConfirmationHandler(IEmailService email) : IEventHandler<OrderPlacedEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public Task Handle(OrderPlacedEvent placed) => email.ConfirmOrder(placed.OrderId);
}

public class ReserveStockHandler(IInventory inventory) : IEventHandler<OrderPlacedEvent>
{
    public Task Handle(OrderPlacedEvent placed) => inventory.Reserve(placed.OrderId);
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

**Dispatch is fire-and-forget on all three, deliberately.** In-process handlers run on a background task
rather than blocking the caller, because that is what Kafka and SQS do, and a handler that only works when
dispatch blocks would break on the day you switch. Treat an event as a notification: anything that *must*
happen belongs in the use case, and a handler should be safe to run twice. Each transport's README states
what it does and does not guarantee.

## Time, ids and jobs

`IClock` instead of `DateTime.Now`, so a test can move time rather than wait for it. `IKeyGenService` gives
Snowflake ids — 64-bit, time-sortable, assigned before the insert, so a caller knows an entity's id without
a round trip and two instances never collide without coordinating. For keys that appear in a URL there are
unguessable string keys instead.

Background jobs schedule from the clock rather than from when the last run finished, so an hourly job stays
on the hour instead of drifting by however long each run took:

```csharp
public class ExpireHoldsJob(IServiceScopeFactory scopes, IClock clock, ILogger<ExpireHoldsJob> logger)
    : ScheduledBackgroundService(scopes, new IntervalSchedule(TimeSpan.FromHours(1)), null, clock, logger)
{
    protected override string ScheduleKey => "expire-holds";
    protected override string ServiceName => nameof(ExpireHoldsJob);

    protected override async Task ExecuteScheduledTask(CancellationToken stoppingToken)
    {
        // A scope per run, because the job outlives any one of them.
        await using var scope = scopes.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IExpireHoldsUseCase>().Execute();
    }
}
```

`TimeOfDaySchedule` is the other one, and takes a timezone, so "2am local" survives a daylight-saving
change. Every instance wakes on the same boundary and takes a distributed lock named for the job, so only
one runs it — the defaults suit a single machine, and running several means registering a lock provider and
a schedule store that they share.

## Errors

A use case throws what it means. `NotFoundException` from the domain becomes a 404 with a problem+json body;
a `ValidationException` or a bad argument becomes a 400; anything unexpected becomes a 500 whose detail is
logged rather than returned, because an unhandled exception's message routinely contains a connection string
or a row nobody should see. `HttpException` and its subclasses are there for the times a use case genuinely
means a status code.

```csharp
public Task<Order> ById(long id) => orders.GetOrThrow(id);   // 404 if it is not there
```

The body carries the same request id as the `X-Request-ID` header, so an id quoted in a bug report finds the
request in the logs.

## The packages

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

## An optional layout

Everything above works in any project, arranged however you like. This is the arrangement it was designed
for — one deployable service whose domain grows as a tree, each part owning its use cases, entities,
endpoints and tests:

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

**[The full guide →](docs/repository-layout.md)** — domains, subdomains, app libraries, entities, and what
the tool wires versus what it leaves you. **[Template overrides →](docs/template-overrides.md)** — every
file it generates comes from a template you can replace, one at a time, keeping the rest.

## Requirements

The .NET 10 SDK. The libraries target net9.0; the CLI, the tests and generated repositories target net10.0.
PostgreSQL for the data packages. Docker only for `MagicCSharp.Testing.Database`.

## Going further

Each package's README covers its own surface — start from the table above. Beyond those:

- **[The example project](https://github.com/MagicDoorInc/MagicCSharp-ExampleProject)** — two services, a
  domain with a subdomain, a shared event contract, and tests at three levels: a use case with fakes, the
  service end to end, and the repository against a real database.
- **[The `mcs` reference](src/MagicCSharp.Cli/)** — every command and what it does.
- **[CHANGELOG](CHANGELOG.md)** — including how to migrate across a breaking version.

## Where it comes from

These are the packages and the structure MagicDoor's backend is built on, extracted so they can be used
outside it, and shaped by building systems at Amazon and Disney before that. The opinions are not
theoretical — they are what was left after finding out which pieces survive a codebase getting large and a
team changing.

That is offered as an explanation of why the decisions look like this, not as a reason to trust them. Where
a decision has a cost, the cost is written next to it. Judge them on that.

## Contributing

The framework is small on purpose — issues, template changes and new `validate` rules are all welcome.
[CONTRIBUTING.md](CONTRIBUTING.md) covers building it, running the tests without Docker, and what to raise
before writing code. [GOVERNANCE.md](GOVERNANCE.md) says who maintains it and what `0.x` means for breaking
changes. To report a vulnerability see [SECURITY.md](SECURITY.md) — please do not open a public issue.

## License

MIT. See [LICENSE](LICENSE).
