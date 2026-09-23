# MagicCSharp.Events.Kafka

The Kafka transport for `IEventDispatcher`. `Dispatch` produces to a topic and returns; a hosted consumer in
the same application subscribes to that topic and runs your handlers. Nothing that publishes or handles
changes from the in-process transport — one registration does.

Add it when handlers should run in another process, or in several, and you already run Kafka.

```bash
dotnet add package MagicCSharp.Events.Kafka
```

## Registration

```csharp
var kafkaConfig = new KafkaMagicEventConfiguration(
    BootstrapServers: "kafka1:9092,kafka2:9092",
    GroupId: "shop",
    Topic: "shop-events");

builder.Services.AddMagicKafkaEvents(kafkaConfig);
builder.AddMagicApp();      // or AddMagicCSharp and friends — the in-process dispatcher steps aside
```

That registers handler discovery (`AddMagicEvents`, idempotent), a producer, a consumer with auto-commit and
auto-offset-store off, `KafkaEventDispatcher` as `IEventDispatcher`, and `KafkaEventsBackgroundService` as
the consumer. Kafka's own log lines flow through `ILogger` at the level they were emitted — emergency, alert
and critical to Critical, error to Error, warning to Warning, notice and info to Information, debug to Debug.
`useOpenTelemetryMetrics: true` turns on the event metrics.

One call per application. Two services that should not see each other's events use different topics; two
instances of the same service share a `GroupId` and Kafka splits the partitions between them.

## What is and is not guaranteed

Worth being precise, because "at-least-once" is usually claimed and rarely true end to end.

**Producing.** `Dispatch` does not block on the broker — it hands the message to the producer and returns,
so the use case is not waiting on a network round trip. A produce that fails is logged as an error naming
the event and topic. It is **not** retried and the caller is not told, so an event raised while the broker
is unreachable is lost. If an event must not be lost, write it to your own database in the same transaction
as the change that caused it and publish from there.

**Consuming.** The listener commits the offset by hand, only after the message has been processed. A message
it cannot parse — including one whose event type this application does not yet know, as happens mid rolling
deploy — is logged as a warning, committed and skipped, because parsing it again would fail again. The SQS
listener does the same, by deleting it. A message that throws while it is being handled is not committed;
Kafka commits are positional, so the next message on the partition that succeeds moves the offset past it,
and it is redelivered only if the consumer restarts or rebalances before that.

**Handler failures do not reach the transport.** `AsyncEventDispatcher` catches whatever a handler throws,
reports it to metrics and logs it, then carries on to the next handler — one failing handler must not stop
the others. From the transport's point of view the message succeeded, and the offset is committed. So the
honest description is **at-least-once delivery to the dispatcher, at-most-once per handler**. A handler that
must not miss work should record its own progress and be safe to re-run.

## How it works

```
┌─────────────┐         ┌─────────┐         ┌─────────────┐
│  Service A  │ Produce │  Kafka  │ Consume │  Service B  │
│             ├────────►│  Topic  ├────────►│             │
│ (Producer)  │         │         │         │ (Consumer)  │
└─────────────┘         └─────────┘         └─────────────┘
                                                    │
                                                    ▼
                                            ┌──────────────┐
                                            │Event Handlers│
                                            └──────────────┘
```

**Producer** — `KafkaEventDispatcher`:

1. Serializes the event with its type name, the same wire format every transport uses
2. `ProduceAsync` to the configured topic, not awaited
3. Returns. A faulted produce is logged: "Failed to produce OrderPlacedEvent to shop-events. The event was NOT published"

**Consumer** — `KafkaEventsBackgroundService`:

1. Subscribes to the topic in the configured group
2. Consumes with a five-second timeout, so the poll interval stays alive and the client's own reconnection
   can run
3. Sets a fresh request id for the message
4. Deserializes, then hands the event to `IAsyncEventDispatcher`, which runs every handler in priority order
   and waits for them
5. Commits the offset

A fatal `ConsumeException` waits five seconds and continues; a non-fatal one waits one; anything unexpected
waits thirty. Cancellation closes the consumer cleanly.

## Custom listeners

The same consumer loop is available for a topic that does not carry `MagicEvent`s — a partner feed, a
change-data-capture stream:

```csharp
public class InventoryFeedListener(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<InventoryFeedListener> logger)
    : KafkaListenerBase<InventoryUpdate>(serviceScopeFactory, logger)
{
    protected override string Topic => "inventory-feed";

    protected override InventoryUpdate? ParseCallback(string body, CancellationToken cancellationToken)
    {
        return JsonSerializer.Deserialize<InventoryUpdate>(body);
    }

    protected override async Task OnMessage(InventoryUpdate inventoryUpdate, CancellationToken cancellationToken)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IApplyInventoryUpdateUseCase>().Execute(inventoryUpdate);
    }
}
```

```csharp
services.AddHostedService<InventoryFeedListener>();
```

It resolves the same `IConsumer<Null, string>` registration `AddMagicKafkaEvents` made, so it runs in the
same consumer group against the same brokers, with the same manual commit and error handling.

## Configuration

Development, one broker in Docker Compose:

```csharp
new KafkaMagicEventConfiguration(BootstrapServers: "localhost:9092", GroupId: "shop-dev", Topic: "shop-events")
```

Production, several brokers:

```csharp
new KafkaMagicEventConfiguration(
    BootstrapServers: "kafka1.prod:9092,kafka2.prod:9092,kafka3.prod:9092",
    GroupId: "shop",
    Topic: "shop-events")
```

From configuration:

```json
{
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "GroupId": "shop",
    "Topic": "shop-events"
  }
}
```

```csharp
var kafkaConfig = new KafkaMagicEventConfiguration(
    BootstrapServers: configuration["Kafka:BootstrapServers"]!,
    GroupId: configuration["Kafka:GroupId"]!,
    Topic: configuration["Kafka:Topic"]!);

builder.Services.AddMagicKafkaEvents(kafkaConfig);
```

## Scaling

Run more instances with the same `GroupId` and Kafka assigns each a share of the partitions; a partition is
consumed by one instance at a time, so events on the same partition stay in order. Throughput is bounded by
the partition count, so create the topic with more partitions than you expect to need instances.

## Related packages

- [MagicCSharp.Events](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Events/README.md)
  — `IEventDispatcher`, handlers, priority, the wire format
- [MagicCSharp.Events.SQS](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Events.SQS/README.md)
  — the other transport

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
