# MagicCSharp.Testing

Test doubles for the framework's own seams, so a test of a use case is `new PlaceOrderUseCase(...)` and an
assertion. No test framework dependency, no database, no containers — add `MagicCSharp.Testing.Database`
when you need those.

The fakes for *your* interfaces — `FakeOrdersRepository`, a recording `IEventDispatcher` — are yours to
write, a few lines each against the interface the use case asked for. What ships here is the doubles for
the seams the framework owns:

| Type | Replaces | Why |
|---|---|---|
| `FakeTimeProvider` | `TimeProvider` | .NET's own test clock, brought in by this package. Move time by hand; a thirty-day late fee is testable in milliseconds. |
| `FakeKeyGen` | `IKeyGenService` | Snowflake ids derived from the test's `TimeProvider`, so ids and timestamps agree. |
| `SyncEventDispatcher` | `IEventDispatcher` | Runs handlers inline and records them, so you can assert without sleeping. |
| `InMemoryDistributedLockProvider` | `IDistributedLockProvider` | Real mutual exclusion in-process, re-entrant during inline event dispatch. |
| `TrackingDistributedLockProvider` | `IDistributedLockProvider` | Grants everything, records the names — for asserting *what* was locked. |

And three helpers for your own fakes: `InMemoryRepository` and `InMemorySoftDeleteRepository`, a repository fake that
behaves like the EF bases; `TestServices`, which builds the use case under test from DI with your fakes in it; and
`[GenerateFakeBase]`, a fake whose every member throws until a test scripts it.

```bash
dotnet add package MagicCSharp.Testing
```

## A use case, directly

```csharp
var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

var applyLateFees = new ApplyLateFeesUseCase(new FakeLeasesRepository(), timeProvider);

timeProvider.Advance(TimeSpan.FromDays(31));
await applyLateFees.Execute();
```

`FakeTimeProvider` comes from Microsoft's `Microsoft.Extensions.TimeProvider.Testing`, which this package
references. `SetUtcNow` and `Advance` move it — forward only; it refuses to go back. Because it is .NET's own
abstraction it also fakes what waits on time: `Task.Delay(delay, timeProvider)`, `PeriodicTimer` and timed
`CancellationTokenSource`s complete when the test moves the clock, so a background service's loop can be
tested without waiting for it.

## Wiring a host

For a test that boots the application — `WebApplicationFactory`, or a `ServiceCollection` of your own —
replace the framework's registrations:

```csharp
var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

services.RemoveAll<TimeProvider>();
services.AddSingleton<TimeProvider>(timeProvider);

services.RemoveAll<IKeyGenService>();
services.AddSingleton<IKeyGenService>(new FakeKeyGen(timeProvider));

services.RemoveAll<IDistributedLockProvider>();
services.AddSingleton<IDistributedLockProvider, InMemoryDistributedLockProvider>();

services.RemoveAll<IEventDispatcher>();
services.AddSingleton<IEventDispatcher>(sp =>
    new SyncEventDispatcher(sp.GetRequiredService<IAsyncEventDispatcher>()));
```

## Asserting on events

```csharp
await createOrder.Execute(new CreateOrderRequest { UserId = 1, ProductIds = [2, 3] });

Assert.True(eventDispatcher.HasDispatchedEvent<OrderCreatedEvent>());
Assert.Single(eventDispatcher.GetDispatchedEvents<OrderCreatedEvent>());
Assert.True(eventDispatcher.HasDispatchedEvent<OrderCreatedEvent>(orderCreatedEvent => orderCreatedEvent.UserId == 1));
```

`DispatchedEvents` is everything in order; `ClearDispatchedEvents()` resets between phases of one test.

## One deliberate difference from production

`SyncEventDispatcher` runs handlers inline, inside whatever lock the emitting use case holds. In production
dispatch is fire-and-forget, so handlers run after that lock is released. `InMemoryDistributedLockProvider`
closes the gap: while `SyncEventDispatcher` is dispatching, a handler re-acquiring a name the emitter holds
gets a no-op handle rather than deadlocking. The window is scoped to that one dispatch, so genuinely
concurrent flows still get ordinary mutual exclusion.

## An in-memory repository

A fake repository is mostly the same code every time: a dictionary, the next id, the filter. Derive from
`InMemoryRepository<TEntity, TEdit, TFilter>` (or `InMemorySoftDeleteRepository` for soft delete) and implement your
repository interface:

```csharp
public class ParcelsRepository(TimeProvider timeProvider, long firstId = 1)
    : InMemorySoftDeleteRepository<Parcel, ParcelEdit, ParcelFilter>(timeProvider, firstId), IParcelsRepository
{
    protected override string? TenantKeyPropertyName => nameof(Parcel.WarehouseId);
}
```

It behaves as `BaseIdRepository` and `BaseIdPaginatedRepository` do on the database: reads newest first; `Created` and
`Updated` from the `TimeProvider`, `Updated` only when something changed; an update or delete of a missing id throws
`NotFoundIdException`; a bulk call checks every id first and writes nothing on a miss; paging honours `IsDisabled`;
`DestroyTenant` removes one tenant's rows when `TenantKeyPropertyName` names the tenant column. A filter is applied
through its `Matches`, which the generator writes for a `partial` filter listing `IEntityFilter<TEntity>`
([MagicCSharp.Data](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data/README.md#generated-filters)),
so the fake narrows exactly as the generated query does.

`SeedStoredRow(edit)` stores a row as the database already holds it, without going through `Create` or its
overrides; `StoredRowsByIdAscending` is every row, soft-deleted too. The entity needs a public `long Id` with a setter
or `init`; the constructor throws naming it otherwise.

**When to write it by hand:** override the one member whose behaviour differs (`CreateEntity`, `CopyWithEdit`,
`Matches`, `ApplyOrder`, `CreateNotFoundException`, or any interface member — all are `virtual`), not the whole class.
A filter without `IEntityFilter` needs a `Matches` override, or the first filtered read throws naming the filter. A
fake for a key repository, or one whose store is not rows, is a class of your own.

## The use case from DI

`TestServices` builds what the test calls the way the host does, with the fakes you give it, so adding a dependency
to a use case does not mean editing every test that constructs it:

```csharp
var testServices = new TestServices(timeProvider)
    .AddFake<IParcelsRepository>(parcelsRepository)
    .AddRegistrations(services => services.AddMagicUseCases(ThisAssemblyOnly));

var shelvedParcel = await testServices.Get<IShelveParcelUseCase>().Execute(parcel.Id);
```

The `TimeProvider` you pass is registered; loggers are `NullLogger`s unless you register others. `AddFake` registers
one instance (a later one for the same service replaces it); `AddRegistrations` takes any other registration.
`Get<T>()` resolves from one scope, building the container on the first call, and a missing dependency throws naming
it. `Create<T>(dependencies)` builds one instance with the given objects in the constructor parameters they fit and
the rest from the container. Registering after the first `Get` or `Create` throws. Resolving through the interface
also runs any `[LogExecuting]` decorator.

## A generated fake base

With `MagicCSharp.Generators` referenced, one line gives an interface a fake base whose every member is
`virtual` and throws `NotScriptedException` ("IOrdersClient.GetOrders(OrderFilter) is not scripted"):

```csharp
[GenerateFakeBase(typeof(IOrdersClient))]
public abstract partial class FakeOrdersClientBase;

public class FakeOrdersClient : FakeOrdersClientBase
{
    public override Task<OrderDto?> GetOrder(long orderId)
    {
        ...
    }
}
```

The fake overrides only what its tests use. A member added to the interface later breaks no fake, and a test
that calls a member nobody scripted fails loudly instead of passing on an empty answer. The fake base keeps any
base class you give it. Any interface works, not only clients; a member the class or its bases already implement is
left alone.

**When to write it by hand:** an interface with a generic method is refused (MCS1515); write that fake as a class that
implements the interface. To leave the generated base, write the throwing members yourself and remove the attribute.

## Related packages

- [MagicCSharp.Testing.Clients](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing.Clients/README.md)
  — checks that every client route is served by a controller route
- [MagicCSharp.Testing.Database](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing.Database/README.md)
  — repository tests against a real PostgreSQL in a container
- [MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp/README.md)
  — the seams these replace

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
