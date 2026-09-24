# MagicCSharp.Events.SQS

The AWS SQS transport for `IEventDispatcher`. `Dispatch` sends to a queue and returns; a hosted consumer in
the same application long-polls that queue and runs your handlers. Nothing that publishes or handles changes
from the in-process transport — one registration does.

Add it when handlers should run in another process, or in several, and you are on AWS.

```bash
dotnet add package MagicCSharp.Events.SQS
```

## Registration

The SQS client is yours to register, because how credentials are obtained is yours — an IAM role on EC2, ECS
or EKS; keys on a laptop; a named profile; LocalStack:

```csharp
builder.Services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(RegionEndpoint.USEast1));   // IAM role

var sqsConfig = new SqsMagicEventConfiguration
{
    QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/shop-events",
    MaxNumberOfMessages = 10,    // per receive, 1–10
    WaitTimeSeconds = 20,        // long polling, 0–20
    VisibilityTimeout = 30,      // seconds a received message stays hidden from other consumers
};

builder.Services.AddMagicSqsEvents(sqsConfig);
builder.AddMagicApp();      // or AddMagicCSharp and friends — the in-process dispatcher steps aside
```

The three numbers default to exactly those values, and are validated at registration rather than at the
first receive. `AddMagicSqsEvents` also runs `AddMagicEvents` (handler discovery, idempotent), registers
`SqsEventDispatcher` as `IEventDispatcher` and `SqsEventsBackgroundService` as the consumer.
`shouldUseOpenTelemetryMetrics: true` turns on the event metrics.

For a laptop, `new AmazonSQSClient(new BasicAWSCredentials(accessKey, secretKey), RegionEndpoint.USEast1)`
or `new AmazonSQSClient(new StoredProfileAWSCredentials("shop-dev"), RegionEndpoint.USEast1)`. For
LocalStack:

```csharp
builder.Services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(new AmazonSQSConfig
{
    ServiceURL = "http://localhost:4566",
    AuthenticationRegion = "us-east-1",
}));

var sqsConfig = new SqsMagicEventConfiguration { QueueUrl = "http://localhost:4566/000000000000/shop-events" };
```

## What is and is not guaranteed

**Producing.** `Dispatch` serializes the event and sends it on a background task, so the use case is not
waiting on a network round trip. A send that fails is logged as an error naming the event. It is **not**
retried and the caller is not told, so an event raised while SQS is unreachable is lost. If an event must not
be lost, write it to your own database in the same transaction as the change that caused it and publish from
there.

**Consuming.** A message is deleted only after it has been processed. One that throws on the way through is
left alone, becomes visible again when its visibility timeout expires, and is received again — by this
instance or another. One whose event type this application does not know is deleted, so a queue shared with
a newer publisher does not fill with messages nobody here handles.

**Handler failures do not reach the transport.** `AsyncEventDispatcher` catches whatever a handler throws,
logs it and carries on to the next handler — one failing handler must not stop the others. From SQS's point
of view the message succeeded, so it is deleted rather than returned to the queue. The honest description is
**at-least-once delivery to the dispatcher, at-most-once per handler**. A handler that must not miss work
should record its own progress and be safe to re-run. If you need a dead-letter queue to catch handler
failures, the handler has to fail the message itself.

## How it works

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

**Producer** — `SqsEventDispatcher`:

1. Serializes the event with its type name, the same wire format every transport uses
2. `SendMessageAsync` on a background task
3. Returns. A failed send is logged: "Failed to send OrderPlacedEvent to SQS"

**Consumer** — `SqsEventsBackgroundService`:

1. `ReceiveMessageAsync` with long polling, up to `MaxNumberOfMessages` at a time
2. For each message: deserialize, then hand the event to `IAsyncEventDispatcher`, which runs every handler in
   priority order and waits for them
3. `DeleteMessageAsync`
4. A receive that throws is logged and the loop waits thirty seconds before trying again

### Long polling

`WaitTimeSeconds: 20` holds the receive open until a message arrives or twenty seconds pass. With it at 0
every empty poll is a billable request and a message can wait a whole polling interval; with it at 20 an
empty queue costs one request per twenty seconds and a message is picked up the moment it lands. There is
no reason to run lower than 20 in production.

### Batching

`MaxNumberOfMessages` is how many a single receive may return, up to SQS's limit of ten. Messages in a batch
are processed one after the other, so a smaller batch lowers the latency of the last message in it and a
larger one lowers the request count. Ten is right unless a single event is slow enough that the tenth would
sit past its visibility timeout waiting — see below.

### Visibility timeout

Once received, a message is hidden from other consumers for `VisibilityTimeout` seconds. Delete it inside
that window and it is gone; fail to and it reappears, to be received again. So the timeout has to cover the
worst-case time from receive to delete — for the last message of a batch, that is the whole batch. Two to
three times your slowest event is a reasonable starting point. Too short redelivers messages that are still
being processed; too long delays the retry of one that genuinely failed.

### Dead-letter queue

Configure a redrive policy on the queue itself, so a message that has been received more than `maxReceiveCount`
times moves aside for a human rather than looping:

```json
{
  "RedrivePolicy": {
    "deadLetterTargetArn": "arn:aws:sqs:us-east-1:123456789012:shop-events-dlq",
    "maxReceiveCount": 3
  }
}
```

Remember what counts: a message reaches the dead-letter queue only when processing threw before the delete
— a parse failure, a dispatcher failure. A handler that threw is caught, so its message is deleted normally.

## Custom listeners

The same consumer loop is available for a queue that does not carry `MagicEvent`s — an S3 notification
queue, a third party's webhook relay:

```csharp
public class ImportUploadedListener(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ImportUploadedListener> logger)
    : SqsListenerBase<S3Notification>(serviceScopeFactory, logger)
{
    protected override string QueueUrl => "https://sqs.us-east-1.amazonaws.com/123456789012/imports";
    protected override int MaxNumberOfMessages => 5;

    protected override S3Notification? ParseCallback(string body, CancellationToken cancellationToken)
    {
        return JsonSerializer.Deserialize<S3Notification>(body);
    }

    protected override async Task OnMessage(S3Notification s3Notification, CancellationToken cancellationToken)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IImportUploadUseCase>().Execute(s3Notification.Key);
    }
}
```

```csharp
services.AddHostedService<ImportUploadedListener>();
```

`MaxNumberOfMessages`, `WaitTimeSeconds` and `VisibilityTimeout` are virtual with the same defaults as the
event consumer. It resolves the registered `IAmazonSQS`, and deletes on success and on an unparseable
message exactly as the event consumer does.

## From configuration

```json
{
  "AWS": {
    "Region": "us-east-1",
    "SQS": {
      "QueueUrl": "https://sqs.us-east-1.amazonaws.com/123456789012/shop-events",
      "MaxMessages": 10,
      "WaitTime": 20,
      "VisibilityTimeout": 30
    }
  }
}
```

```csharp
builder.Services.AddSingleton<IAmazonSQS>(_ =>
    new AmazonSQSClient(RegionEndpoint.GetBySystemName(configuration["AWS:Region"]!)));

var sqsConfig = new SqsMagicEventConfiguration
{
    QueueUrl = configuration["AWS:SQS:QueueUrl"]!,
    MaxNumberOfMessages = int.Parse(configuration["AWS:SQS:MaxMessages"]!),
    WaitTimeSeconds = int.Parse(configuration["AWS:SQS:WaitTime"]!),
    VisibilityTimeout = int.Parse(configuration["AWS:SQS:VisibilityTimeout"]!),
};

builder.Services.AddMagicSqsEvents(sqsConfig);
```

## Scaling and monitoring

Run more instances and each polls the same queue; the visibility timeout is what stops two of them
processing one message at once. SQS standard queues do not preserve order, and handlers should not assume
it.

The CloudWatch numbers worth an alarm: `ApproximateNumberOfMessagesVisible` (the backlog),
`ApproximateAgeOfOldestMessage` (how far behind you are), and the dead-letter queue's
`ApproximateNumberOfMessagesVisible`, which should be zero.

## Related packages

- [MagicCSharp.Events](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Events/README.md)
  — `IEventDispatcher`, handlers, priority, the wire format
- [MagicCSharp.Events.Kafka](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Events.Kafka/README.md)
  — the other transport

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
