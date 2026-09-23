using MagicCSharp.Events.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.SQS;

/// <summary>
///     Background service that consumes events from AWS SQS and dispatches them to registered handlers.
/// </summary>
public class SqsEventsBackgroundService(
    SqsEventsBackgroundServiceConfig config,
    IAsyncEventDispatcher eventDispatcher,
    IEventSerializer eventSerializer,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SqsEventsBackgroundService> logger) : SqsListenerBase<MagicEvent>(serviceScopeFactory, logger)
{
    protected override string QueueUrl => config.QueueUrl;
    protected override int MaxNumberOfMessages => config.MaxNumberOfMessages;
    protected override int WaitTimeSeconds => config.WaitTimeSeconds;
    protected override int VisibilityTimeout => config.VisibilityTimeout;

    protected override MagicEvent? ParseCallback(string body, CancellationToken cancellationToken)
    {
        return eventSerializer.DeserializeMagicEvent(body);
    }

    protected override async Task OnMessage(MagicEvent message, CancellationToken cancellationToken)
    {
        logger.LogTrace("Received event {EventId} of type {EventType} at {OccurredOn}", message.EventId,
            message.GetType().Name, message.OccurredOn);

        // Waiting is to ensure that the event can be safely executed when the application is shutting down,
        // so that it is not lost and race conditions are less likely to occur
        await eventDispatcher.Dispatch(message);
    }
}