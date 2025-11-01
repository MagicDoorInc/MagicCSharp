# MagicCSharp.Events.SQS

**AWS SQS event dispatching for cloud-native applications**

Process events across AWS infrastructure with guaranteed delivery, long polling efficiency, and automatic message handling. MagicCSharp.Events.SQS handles all the SQS complexity so you can focus on your business logic.

## Why MagicCSharp.Events.SQS?

✅ **Serverless-Friendly** - Perfect for Lambda and container-based apps
✅ **Cost-Effective** - Long polling reduces empty receives
✅ **Reliable Delivery** - Messages are only deleted after successful processing
✅ **Auto-Configuration** - Producer and consumer setup handled for you
✅ **Configurable Batching** - Control batch sizes for optimal performance
✅ **AWS Native** - Integrates seamlessly with AWS ecosystem

## Installation

```bash
dotnet add package MagicCSharp.Events.SQS
dotnet add package MagicCSharp.Events
dotnet add package MagicCSharp
dotnet add package AWSSDK.SQS
```

## Quick Start

### 1. Register AWS SQS Client

```csharp
using Amazon.SQS;

// Register IAmazonSQS (this is required)
services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(
    new BasicAWSCredentials(accessKey, secretKey),
    RegionEndpoint.USEast1
));
```

### 2. Configure SQS Events

```csharp
var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: "https://sqs.us-east-1.amazonaws.com/123456789/my-events-queue",
    MaxNumberOfMessages: 10,      // Receive up to 10 messages per poll
    WaitTimeSeconds: 20,           // Long polling (reduces costs)
    VisibilityTimeout: 30          // 30 seconds to process before redelivery
);
```

### 3. Register SQS Events

```csharp
// In your Startup.cs or Program.cs
services.RegisterMagicSQSEvents(sqsConfig);

// Optional: Enable OpenTelemetry metrics
// services.RegisterMagicSQSEvents(sqsConfig, useOpenTelemetryMetrics: true);
```

### 4. Dispatch Events

```csharp
public class OrderService(IEventDispatcher eventDispatcher)
{
    public async Task CreateOrder(CreateOrderRequest request)
    {
        var order = await SaveOrder(request);

        // Event is sent to SQS and returns immediately
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

### 5. Events Are Automatically Consumed

The background service automatically polls SQS and dispatches events to your handlers:

```csharp
public class SendConfirmationHandler(IEmailService emailService)
    : IEventHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent @event)
    {
        // This executes when the event is consumed from SQS
        await emailService.SendOrderConfirmation(@event.OrderId);
    }
}
```

## How It Works

```
┌─────────────┐         ┌─────────┐         ┌─────────────┐
│  Service A  │  Send   │   SQS   │ Receive │  Service B  │
│             ├────────►│  Queue  ├────────►│             │
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
2. Sends to SQS queue asynchronously
3. Returns immediately (fire-and-forget)

**Consumer (Background Service):**
1. Polls SQS with long polling (up to 20 seconds)
2. Receives batch of messages (configurable size)
3. Deserializes events
4. Dispatches to `IAsyncEventDispatcher` (local async dispatcher)
5. Executes all registered handlers
6. Deletes message only after successful processing

## Features

### 🚀 Long Polling for Efficiency

Long polling reduces costs and latency:

```csharp
var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: queueUrl,
    WaitTimeSeconds: 20  // Wait up to 20 seconds for messages
);
```

**Benefits:**
- ✅ Reduces number of empty receives (lower costs)
- ✅ Reduces latency (near real-time processing)
- ✅ Fewer API calls to AWS

**Without long polling (WaitTimeSeconds = 0):**
```
Poll 1: Empty (charge)
Poll 2: Empty (charge)
Poll 3: Empty (charge)
Poll 4: Message! (charge + process)
```

**With long polling (WaitTimeSeconds = 20):**
```
Poll 1: [waiting... message arrives] Message! (charge + process)
```

### 📦 Configurable Batching

Control how many messages to receive per poll:

```csharp
var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: queueUrl,
    MaxNumberOfMessages: 10  // Receive up to 10 messages at once
);
```

**Small Batches (1-3 messages):**
- Lower latency per message
- Good for real-time requirements
- Higher per-message cost

**Large Batches (10 messages):**
- Higher throughput
- More cost-effective
- Good for high-volume processing

### ⏱️ Visibility Timeout Management

Messages become invisible during processing:

```csharp
var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: queueUrl,
    VisibilityTimeout: 30  // 30 seconds to process
);
```

**How it works:**
1. Message received from SQS
2. Message becomes invisible for 30 seconds
3. Processing starts
4. **Success:** Message deleted (won't reappear)
5. **Failure:** After 30 seconds, message becomes visible again (automatic retry)

**Choosing the right timeout:**
- Too short: Messages redelivered while still processing
- Too long: Slow retry on failures
- Good default: 2-3x your average processing time

### 🔄 Automatic Message Deletion

Messages are only deleted after successful processing:

```csharp
try
{
    var message = DeserializeEvent(sqsMessage.Body);
    await eventDispatcher.Dispatch(message);

    // ✅ Success - delete message
    await sqsClient.DeleteMessageAsync(queueUrl, sqsMessage.ReceiptHandle);
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to process message");
    // ❌ Not deleted - message will reappear after visibility timeout
}
```

**Benefits:**
- No data loss if processing fails
- Automatic retry on failures
- At-least-once delivery guarantee

### 🛡️ Error Handling

**Unknown Event Types:**
```csharp
var event = DeserializeMagicEvent(json);
if (event == null)
{
    // Event type not registered - delete message to prevent reprocessing
    await DeleteMessage(receiptHandle);
    continue;
}
```

**Processing Errors:**
```csharp
try
{
    await ProcessEvent(event);
    await DeleteMessage(receiptHandle);  // ✅ Success
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to process");
    // ❌ Not deleted - will retry after visibility timeout
}
```

**SQS Connection Errors:**
```csharp
try
{
    var messages = await sqsClient.ReceiveMessageAsync(...);
    // ... process
}
catch (Exception ex)
{
    logger.LogError(ex, "SQS error");
    await Task.Delay(TimeSpan.FromSeconds(30));  // Wait before retry
}
```

### 🎯 Dead Letter Queues

Configure a dead letter queue in AWS for messages that fail repeatedly:

```json
// SQS Queue Configuration (AWS Console or CloudFormation)
{
  "RedrivePolicy": {
    "deadLetterTargetArn": "arn:aws:sqs:us-east-1:123456789:my-dlq",
    "maxReceiveCount": 3
  }
}
```

After 3 failed attempts, messages move to the DLQ for manual investigation.

## AWS Credentials Setup

### Option 1: IAM Role (Recommended for AWS)

```csharp
// When running on EC2, ECS, Lambda - credentials are automatic
services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(
    RegionEndpoint.USEast1
));
```

### Option 2: Access Keys (Local Development)

```csharp
services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(
    new BasicAWSCredentials(accessKey, secretKey),
    RegionEndpoint.USEast1
));
```

### Option 3: AWS Profile (Local Development)

```csharp
var credentials = new StoredProfileAWSCredentials("my-profile");
services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(
    credentials,
    RegionEndpoint.USEast1
));
```

## Custom SQS Listeners

You can create custom SQS listeners for non-event use cases:

```csharp
public class OrderNotificationListener(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<OrderNotificationListener> logger)
    : SqsListenerBase<OrderNotification>(serviceScopeFactory, logger)
{
    protected override string QueueUrl => "https://sqs.us-east-1.amazonaws.com/.../notifications";
    protected override int MaxNumberOfMessages => 5;
    protected override int WaitTimeSeconds => 20;

    protected override OrderNotification? ParseCallback(string body, CancellationToken cancellationToken)
    {
        return JsonSerializer.Deserialize<OrderNotification>(body);
    }

    protected override async Task OnMessage(OrderNotification message, CancellationToken cancellationToken)
    {
        await ProcessNotification(message);
    }
}

// Register it
services.AddHostedService<OrderNotificationListener>();
```

## Configuration Examples

### Development (LocalStack)

```csharp
services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(
    new AmazonSQSConfig
    {
        ServiceURL = "http://localhost:4566", // LocalStack
        AuthenticationRegion = "us-east-1"
    }
));

var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: "http://localhost:4566/000000000000/dev-events"
);
```

### Production (AWS)

```csharp
services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(
    RegionEndpoint.USEast1  // Uses IAM role automatically
));

var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: "https://sqs.us-east-1.amazonaws.com/123456789/prod-events",
    MaxNumberOfMessages: 10,
    WaitTimeSeconds: 20,
    VisibilityTimeout: 60
);
```

### With Configuration Binding

```csharp
// appsettings.json
{
  "AWS": {
    "Region": "us-east-1",
    "SQS": {
      "QueueUrl": "https://sqs.us-east-1.amazonaws.com/123456789/events",
      "MaxMessages": 10,
      "WaitTime": 20,
      "VisibilityTimeout": 30
    }
  }
}

// Startup.cs
services.AddSingleton<IAmazonSQS>(sp => new AmazonSQSClient(
    RegionEndpoint.GetBySystemName(configuration["AWS:Region"]!)
));

var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: configuration["AWS:SQS:QueueUrl"]!,
    MaxNumberOfMessages: int.Parse(configuration["AWS:SQS:MaxMessages"]!),
    WaitTimeSeconds: int.Parse(configuration["AWS:SQS:WaitTime"]!),
    VisibilityTimeout: int.Parse(configuration["AWS:SQS:VisibilityTimeout"]!)
);

services.RegisterMagicSQSEvents(sqsConfig);
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
services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(RegionEndpoint.USEast1));

var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: "https://sqs.us-east-1.amazonaws.com/123456789/order-events",
    MaxNumberOfMessages: 10,
    WaitTimeSeconds: 20,
    VisibilityTimeout: 30
);

services.RegisterMagicSQSEvents(sqsConfig);

// 4. Dispatch Events (in your service)
public class OrderService(IEventDispatcher eventDispatcher, IOrderRepository orderRepository)
{
    public async Task<Order> CreateOrder(CreateOrderRequest request)
    {
        var order = await orderRepository.Create(request);

        // Send to SQS
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

## Best Practices

### 1. Set Appropriate Visibility Timeout

```csharp
// Calculate based on your average processing time
var avgProcessingTime = TimeSpan.FromSeconds(10);
var visibilityTimeout = (int)(avgProcessingTime.TotalSeconds * 3); // 3x buffer

var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: queueUrl,
    VisibilityTimeout: visibilityTimeout
);
```

### 2. Use Long Polling

```csharp
// Always use long polling to reduce costs
var sqsConfig = new SqsMagicEventConfiguration(
    QueueUrl: queueUrl,
    WaitTimeSeconds: 20  // Maximum long polling duration
);
```

### 3. Configure Dead Letter Queues

Set up DLQs in AWS to catch messages that fail repeatedly:
- MaxReceiveCount: 3-5 (retry 3-5 times before sending to DLQ)
- Monitor DLQ for failed messages
- Investigate and fix issues
- Replay messages from DLQ when fixed

### 4. Monitor CloudWatch Metrics

Key metrics to monitor:
- `ApproximateNumberOfMessagesVisible` - Queue backlog
- `ApproximateAgeOfOldestMessage` - Processing lag
- `NumberOfMessagesSent` - Production rate
- `NumberOfMessagesDeleted` - Processing rate

## Scaling

**Scale Consumers Horizontally:**

Run multiple instances to process messages in parallel:

```
Instance 1: Polls queue → Processes 10 messages
Instance 2: Polls queue → Processes 10 messages
Instance 3: Polls queue → Processes 10 messages
```

SQS ensures each message is delivered to only one consumer at a time (via visibility timeout).

## Related Packages

**[MagicCSharp.Events](https://www.nuget.org/packages/MagicCSharp.Events)** - Core events library (required)
**[MagicCSharp.Events.Kafka](https://www.nuget.org/packages/MagicCSharp.Events.Kafka)** - Kafka alternative
**[MagicCSharp](https://www.nuget.org/packages/MagicCSharp)** - Core infrastructure library (required)

## License

MIT License - See LICENSE file for details.
