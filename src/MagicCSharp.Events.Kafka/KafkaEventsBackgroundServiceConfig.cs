namespace MagicCSharp.Events.Kafka;

/// <summary>
///     Configuration for the Kafka events background service.
/// </summary>
public record KafkaEventsBackgroundServiceConfig
{
    /// <summary>The Kafka topic to consume from and produce to.</summary>
    public required string Topic { get; init; }
}
