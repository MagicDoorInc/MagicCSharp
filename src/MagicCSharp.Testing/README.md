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

## Related packages

- [MagicCSharp.Testing.Database](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing.Database/README.md)
  — repository tests against a real PostgreSQL in a container
- [MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp/README.md)
  — the seams these replace

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
