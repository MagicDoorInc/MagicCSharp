namespace MagicCSharp.Events;

/// <summary>
/// Base record for all events in the MagicCSharp event system.
/// </summary>
public abstract record MagicEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when this event occurred.
    /// </summary>
    public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow;
}
