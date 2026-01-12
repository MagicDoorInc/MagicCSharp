using Amazon.SQS;
using Amazon.SQS.Model;
using MagicCSharp.Events.Events;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.SQS;

/// <summary>
///     Event dispatcher that sends events to an AWS SQS queue.
/// </summary>
public class SqsEventDispatcher(
    SqsEventsBackgroundServiceConfig config,
    IAmazonSQS sqsClient,
    IEventSerializer eventSerializer,
    ILogger<SqsEventDispatcher> logger) : IEventDispatcher
{
    public void Dispatch(MagicEvent? magicEvent)
    {
        if (magicEvent is null)
        {
            return;
        }

        logger.LogTrace("Dispatching event {EventType} to SQS", magicEvent.GetType().Name);

        var serializedEvent = eventSerializer.SerializeMagicEvent(magicEvent);

        // Fire and forget - queue the message
        _ = Task.Run(async () =>
        {
            try
            {
                await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = config.QueueUrl,
                    MessageBody = serializedEvent,
                });
            } catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send event {EventType} to SQS", magicEvent.GetType().Name);
            }
        });

        // SQS events background service will handle the rest
    }
}