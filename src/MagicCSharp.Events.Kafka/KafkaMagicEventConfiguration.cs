namespace MagicCSharp.Events.Kafka;

/// <summary>
///     Configuration for Kafka event dispatching and consuming.
/// </summary>
public record KafkaMagicEventConfiguration
{
    /// <summary>Kafka bootstrap servers (comma-separated list of host:port).</summary>
    public required string BootstrapServers { get; init; }

    /// <summary>Kafka consumer group ID.</summary>
    public required string GroupId { get; init; }

    /// <summary>Kafka topic for events.</summary>
    public required string Topic { get; init; }
}
