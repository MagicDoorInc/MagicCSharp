# MagicCSharp.Testing

Test doubles for the MagicCSharp framework. No test framework dependency, no database, no containers — add
`MagicCSharp.Testing.Database` when you need those.

| Type | Replaces | Why |
|---|---|---|
| `FakeClock` | `IClock` | Move time by hand. A thirty-day late fee is testable in milliseconds. |
| `FakeKeyGen` | `IKeyGenService` | Snowflake ids derived from `FakeClock`, so ids and timestamps agree. |
| `SyncEventDispatcher` | `IEventDispatcher` | Runs handlers inline and records them, so you can assert without sleeping. |
| `InMemoryDistributedLockProvider` | `IDistributedLockProvider` | Real mutual exclusion in-process, re-entrant during inline event dispatch. |
| `TrackingDistributedLockProvider` | `IDistributedLockProvider` | Grants everything, records the names — for asserting *what* was locked. |

## Wiring

```csharp
var clock = new FakeClock();

services.RemoveAll<IClock>();
services.AddSingleton<IClock>(clock);

services.RemoveAll<IKeyGenService>();
services.AddSingleton<IKeyGenService>(new FakeKeyGen(clock));

services.RemoveAll<IDistributedLockProvider>();
services.AddSingleton<IDistributedLockProvider, InMemoryDistributedLockProvider>();

services.RemoveAll<IEventDispatcher>();
services.AddSingleton<IEventDispatcher>(sp =>
    new SyncEventDispatcher(sp.GetRequiredService<IAsyncEventDispatcher>()));
```

## Asserting on events

```csharp
clock.SetTime(2026, 3, 1);
await createOrder.Execute(new CreateOrderRequest(userId: 1, productIds: [2, 3]));

Assert.True(events.HasDispatchedEvent<OrderCreated>());
Assert.Single(events.GetDispatchedEvents<OrderCreated>());
```

## One deliberate difference from production

`SyncEventDispatcher` runs handlers inline, inside whatever lock the emitting use case holds. In production
dispatch is fire-and-forget, so handlers run after that lock is released. `InMemoryDistributedLockProvider`
closes the gap: while `SyncEventDispatcher` is dispatching, a handler re-acquiring a name the emitter holds
gets a no-op handle rather than deadlocking. The window is scoped to that one dispatch, so genuinely
concurrent flows still get ordinary mutual exclusion.
