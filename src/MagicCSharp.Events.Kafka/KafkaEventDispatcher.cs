using Confluent.Kafka;
using MagicCSharp.Events.Events;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.Kafka;

/// <summary>
///     Event dispatcher that sends events to a Kafka topic.
/// </summary>
public class KafkaEventDispatcher(
    KafkaEventsBackgroundServiceConfig config,
    IProducer<Null, string> kafkaProducer,
    IEventSerializer eventSerializer,
    ILogger<KafkaEventDispatcher> logger) : IEventDispatcher
{
    public void Dispatch(MagicEvent? magicEvent)
    {
        if (magicEvent is null)
        {
            return;
        }

        logger.LogTrace("Dispatching event {EventType}", magicEvent.GetType().Name);

        // Deliberately not awaited: dispatching must not block the use case that raised the event. But the
        // task is observed — discarding it meant a broker that rejected the message failed in complete
        // silence, which is the worst way for an event to go missing.
        var produce = kafkaProducer.ProduceAsync(config.Topic, new Message<Null, string>
        {
            Value = eventSerializer.SerializeMagicEvent(magicEvent),
        });

        _ = produce.ContinueWith(
            task => logger.LogError(task.Exception,
                "Failed to produce {EventType} to {Topic}. The event was NOT published",
                magicEvent.GetType().Name, config.Topic),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}