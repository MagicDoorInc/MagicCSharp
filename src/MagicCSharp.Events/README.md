# MagicCSharp.Events

"This happened — carry on." One interface to publish, one to handle, and a transport chosen at registration:
in-process here, Kafka or SQS from the packages beside it. Business code depends on `IEventDispatcher` and
never on a broker.

Add it when a use case has side work that should not block it — the confirmation email, the search index,
the metadata, the next workflow — and you want the person writing the use case to stop thinking about
pipelines.

```bash
dotnet add package MagicCSharp.Events
```

## Publish, handle, register

An event is a record carrying ids and primitives:

```csharp
public record OrderPlacedEvent : MagicEvent
{
    public required long OrderId { get; init; }
    public required long CustomerId { get; init; }
}
```

Never an entity. An event will be deserialized by code built from a different commit than the one that
published it, so a property typed as an entity ties the wire format to that entity's shape — `mcs validate`
flags it. `MagicEvent` supplies `EventId` and `OccurredOn`.

A handler is a class. Implement the interface and it is discovered, exactly as use cases are:

```csharp
public class SendConfirmationHandler(IEmailService emailService) : IEventHandler<OrderPlacedEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public async Task Handle(OrderPlacedEvent orderPlacedEvent)
    {
        await emailService.ConfirmOrder(orderPlacedEvent.OrderId);
    }
}
```

Anything that depends on `IEventDispatcher` can publish:

```csharp
public class PlaceOrderUseCase(
    IOrdersRepository ordersRepository,
    IEventDispatcher eventDispatcher) : IPlaceOrderUseCase
{
    public async Task<Order> Execute(PlaceOrderRequest request)
    {
        var order = await ordersRepository.Create(new OrderEdit { ... });

        eventDispatcher.Dispatch(new OrderPlacedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
        });

        return order;
    }
}
```

The transport is one registration, and the only line that differs between running locally and running on a
queue:

```csharp
services.AddLocalMagicEvents();               // in-process
services.AddMagicKafkaEvents(kafkaConfig);    // MagicCSharp.Events.Kafka
services.AddMagicSqsEvents(sqsConfig);        // MagicCSharp.Events.SQS
```

Each of those also runs `AddMagicEvents()`, which discovers every `IEventHandler<T>`, registers the serializer
and `IAsyncEventDispatcher`, and is idempotent. Call `AddMagicEvents` directly only when registering a
transport of your own: it does not register `IEventDispatcher`, so on its own the application fails at
resolution the first time anything dispatches. `MagicCSharp.App` registers the in-process transport unless
something already claimed `IEventDispatcher`, so `AddMagicKafkaEvents` before `AddMagicApp` does what you
expect.

## What Dispatch promises

`Dispatch` returns immediately on every transport. In-process, handlers run on a background task; on Kafka or
SQS, the message is handed to the producer and handlers run in a consumer somewhere else. A local dispatcher
that blocked until every handler finished would make the same use case behave one way in development and
another in production — a handler that re-enters a lock the emitter holds would deadlock only after the
switch to Kafka. Keeping both asynchronous is what makes swapping the registration a configuration change
rather than a rewrite. In-flight dispatches are drained on process exit, so a short-lived process does not
lose events it already accepted.

That sets the contract for everything that publishes and everything that handles:

- **An event is a notification, not a step.** Anything that must happen for the operation to be true belongs
  in the use case. `Dispatch` returning does not mean the email was sent.
- **Handlers are eventually consistent side processes.** Email, metadata, search, the next workflow. Lag is
  normal, and an order can exist before its confirmation does.
- **A handler should be safe to run twice.** Any real transport delivers at least once, and a handler that
  failed halfway is a handler that will run again.
- **Retries, dead letters and delivery are the adapter's job.** Whoever builds the transport owns them; the
  code that raised the event does not know they exist. Each transport package states precisely what it
  guarantees.
- **The one operation that needs a particular bus takes it directly.** That is an explicit extra dependency
  on one use case, not a reason for every use case to learn the bus.

Publishers and handlers do not change when the transport does. Publishing to Kafka and SQS at once is not a
registration the framework ships; it is a composite `IEventDispatcher` you write, a few lines against the
same interface — and publishers and handlers still do not change.

## Priority

Several handlers for one event run in sequence, in the same process, ordered by `Priority` — lower first:

| Priority | Value | For |
|---|---|---|
| `Cron` | -1 | scheduled work |
| `AddDataNoDependencies` | 0 | creating data that depends on nothing — the default |
| `AddDataWithDependencies` | 1000 | creating data that depends on an earlier handler's |
| `UpdateMetadata` | 2000 | denormalized fields, counters |
| `DeleteData` | 2500 | removals |
| `NotifyUser` | 3000 | email, push |
| `RunLast` | 10000 | anything that should see everything else done |

**`Priority` must be `static`.** The registration reads it without constructing the handler, so an instance
property compiles and is silently ignored — the handler runs at the default. The interface declares it
`static virtual`.

## Handlers

One class can handle several events: implement `IEventHandler<T>` once per type. Handlers are registered
transient and resolved from a scope opened per event, so a scoped repository in a handler's constructor is
fine and no state survives between events.

A handler that throws is logged and counted, and the next handler still runs — one failing handler must not
stop the others. The consequence, spelled out in each transport's README, is that the transport sees the
message as handled.

The dispatcher opens a logging scope per event, using the last twelve characters of the event id, and one per
handler, so a log line from three handlers deep still says which event it belongs to.

## On the wire

Every transport serializes the same way:

```json
{
  "type": "OrderPlacedEvent",
  "body": {
    "orderId": "42",
    "customerId": "7",
    "eventId": "3f2c1a9e-8b7d-4e6f-9a0b-1c2d3e4f5a6b",
    "occurredOn": "2026-03-01T10:30:00+00:00"
  }
}
```

The type's simple name is the discriminator, so two event types with the same name is a startup error naming
both. Longs go as strings so a JavaScript consumer does not lose precision. Enums go by name. A message whose
type this application does not know deserializes to nothing — a consumer only handles the events it knows —
and unknown properties are ignored, which is what lets a publisher add a field before every consumer is
redeployed.

## Metrics

```csharp
services.AddLocalMagicEvents(useOpenTelemetryMetrics: true);
```

A `MagicCSharp.Events` meter with `Events` (received, by type), `Events.Failed` (by type and handler),
`Events.Finished` and an `Events.ExecutionTime` histogram in milliseconds. Off by default; the null handler
costs nothing.

## Testing

A use case under test takes a recording fake of `IEventDispatcher` that you write — the interface is one
method. A handler under test is constructed directly with fakes of its own dependencies, and `Handle` is
awaited. No dispatcher is involved in either.

When a test needs handlers to have *run* before the assertion — "placing the order updated the projection"
—
[MagicCSharp.Testing](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing/README.md)
ships `SyncEventDispatcher`, which runs them inline and records what was dispatched:

```csharp
await placeOrder.Execute(request);

Assert.True(eventDispatcher.HasDispatchedEvent<OrderPlacedEvent>());
Assert.Single(eventDispatcher.GetDispatchedEvents<OrderPlacedEvent>());
```

## Transports

- [MagicCSharp.Events.Kafka](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Events.Kafka/README.md)
  — a topic per application, a consumer group, manual commit
- [MagicCSharp.Events.SQS](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Events.SQS/README.md)
  — long polling, a visibility timeout, delete on success

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
