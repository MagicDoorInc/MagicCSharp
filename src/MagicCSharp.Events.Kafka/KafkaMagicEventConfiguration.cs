namespace MagicCSharp.Events.Kafka;

/// <summary>
/// Configuration for Kafka event dispatching and consuming.
/// </summary>
/// <param name="BootstrapServers">Kafka bootstrap servers (comma-separated list of host:port).</param>
/// <param name="GroupId">Kafka consumer group ID.</param>
/// <param name="Topic">Kafka topic for events.</param>
public record KafkaMagicEventConfiguration(
    string BootstrapServers,
    string GroupId,
    string Topic);
