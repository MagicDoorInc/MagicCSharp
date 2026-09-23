using MagicCSharp.Events.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.Kafka;

/// <summary>
///     Background service that consumes events from Kafka and dispatches them to registered handlers.
/// </summary>
public class KafkaEventsBackgroundService(
    KafkaEventsBackgroundServiceConfig config,
    IAsyncEventDispatcher eventDispatcher,
    IEventSerializer eventSerializer,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<KafkaEventsBackgroundService> logger) : KafkaListenerBase<MagicEvent>(serviceScopeFactory, logger)
{
    protected override string Topic => config.Topic;

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