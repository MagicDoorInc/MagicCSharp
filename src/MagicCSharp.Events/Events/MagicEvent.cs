namespace MagicCSharp.Events.Events;

/// <summary>
///     Base record for all events in the MagicCSharp event system.
/// </summary>
public abstract record MagicEvent
{
    /// <summary>
    ///     Unique identifier for this event instance.
    /// </summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     Timestamp when this event occurred.
    /// </summary>
#pragma warning disable MCS0008 // an event records when it happened, and has no clock to inject
    public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow; // conventions: allow — an event records when it happened, and has no clock to inject
#pragma warning restore MCS0008
}