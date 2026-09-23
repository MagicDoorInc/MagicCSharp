# Events and Messaging

This guide owns event contracts, publishing and handlers.

## What an event is for

An event says "this happened" so other work can follow without the operation waiting for it: the welcome
email after a lease is signed, a receipt after a payment, a notice after a late fee.

Choose by what the caller needs:

- The caller needs the result, or the operation is not true without it → **call the use case** in the chain.
- It is a consequence that may happen a moment later → **dispatch an event** and handle it.

Do not add an event for a consumer that does not exist yet.

## Contracts

Events live in `Libs/Events/Default/{Domain}/`, named `{Entity}{Action}Event`:

```csharp
public record LeaseSignedEvent : MagicEvent
{
    public required long LeaseId { get; init; }
    public required long PropertyId { get; init; }
}
```

Only ids and primitives — never an entity, a DTO or an app-local type. Whoever handles it may be built from a
different commit than whoever published it; a handler that needs more loads it by id.

## Publishing

Only a use case publishes, and only after its change is saved:

```csharp
var paidCharge = await chargesRepository.Update(charge with { Paid = timeProvider.GetUtcNow() });

eventDispatcher.Dispatch(new ChargePaidEvent
{
    ChargeId = paidCharge.Id,
    LeaseId = paidCharge.LeaseId,
});
```

`Dispatch` is fire-and-forget on every transport and returns `void`; do not await it. Controllers never publish.

## Handlers

A handler lives in the `Default` project of the domain that reacts, under `Events/`, named for what it does
and why: `QueueWelcomeEmailOnLeaseSigned`. It implements `IEventHandler<T>` and is discovered — never
registered by hand.

```csharp
public class QueueReceiptOnChargePaid(
    IGetChargesUseCase getCharges,
    IGetLeasesUseCase getLeases,
    IQueueNotificationUseCase queueNotification) : IEventHandler<ChargePaidEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public async Task Handle(ChargePaidEvent chargePaidEvent)
    {
        // load what it needs by id, then call a use case
    }
}
```

- A handler does its work through use cases, like any other caller.
- **Handlers must be safe to run twice.** Transports deliver at least once. `QueueNotificationUseCase` keys
  each notification by what it is about, so a redelivered event finds the one already queued.
- Handlers are eventually consistent: the operation that published has already returned.
- Set `Priority` when order matters (`NotifyUser` runs after data handlers). Do not rely on discovery order.

## Transport

This service publishes and consumes through Kafka: `AddMagicKafkaEvents(...)` in `Program.cs`, before
`AddMagicApp()`, with the broker from `compose.yaml` and the topic `leasing-events`. The service is both
producer and consumer, so a handler runs a moment after the request that published its event.

Switching transport — in-process, Kafka, SQS — is that one registration; publishers and handlers do not change.
Tests do not use Kafka: `LeasingTestBase` swaps in `SyncEventDispatcher`, which runs handlers before `Dispatch`
returns.

Know what the transport promises. Kafka delivers at least once, so handlers must be idempotent. A handler that
throws is logged and not retried. An event that must never be lost needs an outbox, which the dispatcher does
not provide.
