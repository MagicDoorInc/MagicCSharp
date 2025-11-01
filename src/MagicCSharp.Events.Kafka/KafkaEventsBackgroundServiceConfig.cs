namespace MagicCSharp.Events.Kafka;

/// <summary>
/// Configuration for the Kafka events background service.
/// </summary>
/// <param name="Topic">The Kafka topic to consume from and produce to.</param>
public record KafkaEventsBackgroundServiceConfig(string Topic);
