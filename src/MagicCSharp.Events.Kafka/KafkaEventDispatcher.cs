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

        // TODO: maybe we should use the sync produce here to ensure the event is sent
        kafkaProducer.ProduceAsync(config.Topic, new Message<Null, string>
        {
            Value = eventSerializer.SerializeMagicEvent(magicEvent),
        });

        // Kafka events background service will handle the rest
    }
}