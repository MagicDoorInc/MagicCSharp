# MagicCSharp.Events.Kafka

**Kafka event dispatching for distributed systems**

Process events across multiple services with guaranteed delivery, fault tolerance, and automatic producer/consumer
setup. MagicCSharp.Events.Kafka handles all the Kafka complexity so you can focus on your business logic.

## Why MagicCSharp.Events.Kafka?

✅ **Non-Blocking Produce** - Dispatch returns immediately; failures are logged, not silent

✅ **Fault Tolerant** - Automatic retries and error handling

✅ **Manual Commit** - Auto-commit is off, so the offset moves only after the message is handled

✅ **Auto-Configuration** - Producer and consumer setup handled for you

✅ **Integrated Logging** - Kafka logs flow through ILogger

✅ **Scalable** - Add more consumers to process events faster

## Installation

```bash
dotnet add package MagicCSharp.Events.Kafka
dotnet add package MagicCSharp.Events
dotnet add package MagicCSharp
```

## Quick Start

### 1. Configure Kafka

```csharp
var kafkaConfig = new KafkaMagicEventConfiguration(
    BootstrapServers: "localhost:9092",
    GroupId: "my-service-group",
    Topic: "domain-events"
);
```

### 2. Register Kafka Events

```csharp
// In your Startup.cs or Program.cs
services.RegisterMagicKafkaEvents(kafkaConfig);

// Optional: Enable OpenTelemetry metrics
// services.RegisterMagicKafkaEvents(kafkaConfig, useOpenTelemetryMetrics: true);
```

### 3. Dispatch Events

```csharp
public class OrderService(IEventDispatcher eventDispatcher)
{
    public async Task CreateOrder(CreateOrderRequest request)
    {
        var order = await SaveOrder(request);

        // Event is sent to Kafka and returns immediately
        eventDispatcher.Dispatch(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount
        });

        return order;
    }
}
```

### 4. Events Are Automatically Consumed

The background service automatically consumes events from Kafka and dispatches them to your handlers:

```csharp
public class SendConfirmationHandler(IEmailService emailService)
    : IEventHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent @event)
    {
        // This executes when the event is consumed from Kafka
        await emailService.SendOrderConfirmation(@event.OrderId);
    }
}
```

## How It Works

```
┌─────────────┐         ┌─────────┐         ┌─────────────┐
│  Service A  │ Dispatch│  Kafka  │ Consume │  Service B  │
│             ├────────►│  Topic  ├────────►│             │
│ (Producer)  │         │         │         │ (Consumer)  │
└─────────────┘         └─────────┘         └─────────────┘
                                                    │
                                                    ▼
                                            ┌──────────────┐
                                            │Event Handlers│
                                            └──────────────┘
```

**Producer (IEventDispatcher):**

1. Serializes the event with type information
2. Sends to Kafka topic asynchronously
3. Returns immediately (fire-and-forget)

**Consumer (Background Service):**

1. Polls Kafka for new messages
2. Deserializes events
3. Dispatches to `IAsyncEventDispatcher` (local async dispatcher)
4. Executes all registered handlers
5. Commits offset only after successful processing

## Features

### 🚀 Automatic Producer/Consumer Setup

No need to configure Kafka manually - everything is set up for you:

```csharp
services.RegisterMagicKafkaEvents(new KafkaMagicEventConfiguration(
    BootstrapServers: "kafka1:9092,kafka2:9092",
    GroupId: "order-service",
    Topic: "domain-events"
));
```

**What gets configured:**

- ✅ Kafka producer with connection pooling
- ✅ Kafka consumer with consumer group
- ✅ Background service for consuming
- ✅ Logging adapters for Kafka logs
- ✅ Event serializer with type information

### 🔄 Manual Commit for Reliability

Messages are only committed after successful processing:

```csharp
// Kafka message received
var event = DeserializeEvent(message);

try
{
    await eventDispatcher.Dispatch(event);  // Process all handlers

    consumer.Commit(message);  // ✅ Only commit on success
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to process message");
    // ❌ Message is NOT committed - will be retried
}
```

**Benefits:**

- No data loss if processing fails
- Failed messages are automatically retried
- At-least-once delivery to the dispatcher (see below for what that does and does not mean)

### 📊 Integrated Logging

Kafka logs automatically flow through `ILogger`:

```csharp
// Kafka internal logs appear in your logging system
[2024-01-15 10:30:00] [Information] Kafka: Producer connected to localhost:9092
[2024-01-15 10:30:01] [Debug] Kafka: Message delivered to partition 0, offset 12345
[2024-01-15 10:30:02] [Warning] Kafka: Connection to broker temporarily lost
```

**Log Levels:**

- `Critical` - Emergency/Alert level issues
- `Error` - Kafka errors
- `Warning` - Connection issues, temporary failures
- `Information` - Connection status, messages delivered
- `Debug` - Detailed Kafka operations
- `Trace` - Very detailed debugging

### ⚡ High Performance

**Producer:**

- Async non-blocking sends
- Connection pooling
- Automatic batching

**Consumer:**

- Long polling for efficiency
- Parallel processing (scale with consumer group)
- Manual offset management

### 🔧 Custom Kafka Configuration

Need more control? You can customize the Kafka clients:

```csharp
// After registration, you can access and customize
var producer = serviceProvider.GetRequiredService<IProducer<Null, string>>();
var consumer = serviceProvider.GetRequiredService<IConsumer<Null, string>>();
```

### 🎯 Multiple Topics

You can configure different services to use different topics:

```csharp
// Order Service
services.RegisterMagicKafkaEvents(new KafkaMagicEventConfiguration(
    BootstrapServers: "localhost:9092",
    GroupId: "order-service",
    Topic: "order-events"  // Only order events
));

// User Service
services.RegisterMagicKafkaEvents(new KafkaMagicEventConfiguration(
    BootstrapServers: "localhost:9092",
    GroupId: "user-service",
    Topic: "user-events"  // Only user events
));
```

### 🛡️ Error Handling

The consumer handles errors gracefully:

**Parsing Errors:**

```csharp
// Unknown event type - message is skipped and committed
var event = DeserializeMagicEvent(json);
if (event == null)
{
    // Event type not registered, ignore it
    consumer.Commit(message);
    continue;
}
```

**Processing Errors:**

```csharp
// Handler throws exception - message is NOT committed
try
{
    await ProcessEvent(event);
    consumer.Commit(message);  // ✅ Success
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to process");
    // ❌ Not committed - will retry on next poll
}
```

**Connection Errors:**

```csharp
try
{
    var message = consumer.Consume(cancellationToken);
    // ... process
}
catch (Exception ex)
{
    logger.LogError(ex, "Kafka error");
    await Task.Delay(TimeSpan.FromSeconds(30));  // Wait before retry
}
```

## Custom Kafka Listeners

You can create custom Kafka listeners for non-event use cases:

```csharp
public class OrderNotificationListener(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<OrderNotificationListener> logger)
    : KafkaListenerBase<OrderNotification>(serviceScopeFactory, logger)
{
    protected override string Topic => "order-notifications";

    protected override OrderNotification? ParseCallback(string body, CancellationToken cancellationToken)
    {
        return JsonSerializer.Deserialize<OrderNotification>(body);
    }

    protected override async Task OnMessage(OrderNotification message, CancellationToken cancellationToken)
    {
        // Handle the notification
        await ProcessNotification(message);
    }
}

// Register it
services.AddHostedService<OrderNotificationListener>();
```

## Configuration Examples

### Development (Docker Compose)

```csharp
var kafkaConfig = new KafkaMagicEventConfiguration(
    BootstrapServers: "localhost:9092",
    GroupId: "dev-service",
    Topic: "dev-events"
);
```

### Production (Multiple Brokers)

```csharp
var kafkaConfig = new KafkaMagicEventConfiguration(
    BootstrapServers: "kafka1.prod:9092,kafka2.prod:9092,kafka3.prod:9092",
    GroupId: "order-service-prod",
    Topic: "production-events"
);
```

### With Configuration Binding

```csharp
// appsettings.json
{
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "GroupId": "my-service",
    "Topic": "events"
  }
}

// Startup.cs
var kafkaConfig = new KafkaMagicEventConfiguration(
    BootstrapServers: configuration["Kafka:BootstrapServers"]!,
    GroupId: configuration["Kafka:GroupId"]!,
    Topic: configuration["Kafka:Topic"]!
);

services.RegisterMagicKafkaEvents(kafkaConfig);
```

## Complete Example

```csharp
// 1. Define Event
public record OrderCreatedEvent : MagicEvent
{
    public long OrderId { get; init; }
    public long CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
}

// 2. Create Handler (runs when event is consumed)
public class OrderNotificationHandler(
    IEmailService emailService,
    ILogger<OrderNotificationHandler> logger)
    : IEventHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent @event)
    {
        logger.LogInformation("Sending order confirmation for order {OrderId}", @event.OrderId);
        await emailService.SendOrderConfirmation(@event.OrderId);
    }
}

// 3. Configure (in Startup.cs)
var kafkaConfig = new KafkaMagicEventConfiguration(
    BootstrapServers: "localhost:9092",
    GroupId: "order-service",
    Topic: "domain-events"
);

services.RegisterMagicKafkaEvents(kafkaConfig);

// 4. Dispatch Events (in your service)
public class OrderService(IEventDispatcher eventDispatcher, IOrderRepository orderRepository)
{
    public async Task<Order> CreateOrder(CreateOrderRequest request)
    {
        var order = await orderRepository.Create(request);

        // Send to Kafka
        eventDispatcher.Dispatch(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount
        });

        return order;
    }
}
```

## Scaling

**Scale Consumers Horizontally:**

Run multiple instances with the same `GroupId` to process events in parallel:

```
Instance 1: GroupId="order-service" → Processes partition 0
Instance 2: GroupId="order-service" → Processes partition 1
Instance 3: GroupId="order-service" → Processes partition 2
```

Kafka automatically balances partitions across consumers in the same group.

## Related Packages

**[MagicCSharp.Events](https://www.nuget.org/packages/MagicCSharp.Events)** - Core events library (required)
**[MagicCSharp.Events.SQS](https://www.nuget.org/packages/MagicCSharp.Events.SQS)** - AWS SQS alternative
**[MagicCSharp](https://www.nuget.org/packages/MagicCSharp)** - Core infrastructure library (required)

## License

MIT License - See LICENSE file for details.

## What is and is not guaranteed

Worth being precise, because "at-least-once" is usually claimed and rarely true end to end.

**Producing.** `Dispatch` does not block on the broker — it hands the message to the producer and returns,
so the use case is not waiting on a network round trip. A produce that fails is logged as an error naming
the event and topic. It is **not** retried and the caller is not told, so an event raised while the broker
is unreachable is lost. If an event must not be lost, write it to your own database in the same transaction
as the change that caused it and publish from there.

**Consuming.** The listener commits the offset by hand, only after the message has been processed, so a
message that fails to parse is not committed and is redelivered.

**Handler failures do not reach the transport.** `AsyncEventDispatcher` catches whatever a handler throws,
reports it to metrics and logs it, then carries on to the next handler — one failing handler must not stop
the others. The consequence is that from the transport's point of view the message succeeded, and the
offset is committed. So the honest description is **at-least-once delivery to the dispatcher, at-most-once
per handler**. A handler that must not miss work should record its own progress and be safe to re-run.
