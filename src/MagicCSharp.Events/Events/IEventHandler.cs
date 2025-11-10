namespace MagicCSharp.Events;

/// <summary>
/// Interface for handling events of type T.
/// </summary>
/// <typeparam name="T">The event type to handle.</typeparam>
public interface IEventHandler<T> where T : MagicEvent
{
    /// <summary>
    /// Priority of this event handler. Lower values execute first.
    /// </summary>
    static virtual MagicEventPriority Priority { get; } = MagicEventPriority.AddDataNoDependencies;

    /// <summary>
    /// Handle the event.
    /// </summary>
    Task Handle(T magicEvent);
}

/// <summary>
/// Predefined priority levels for event handlers.
/// </summary>
public enum MagicEventPriority
{
    /// <summary>
    /// For cron or scheduled tasks.
    /// </summary>
    Cron = -1,

    /// <summary>
    /// For adding data with no dependencies on other handlers.
    /// </summary>
    AddDataNoDependencies = 0,

    /// <summary>
    /// For adding data that depends on other handlers completing first.
    /// </summary>
    AddDataWithDependencies = 1000,

    /// <summary>
    /// For updating metadata after data operations.
    /// </summary>
    UpdateMetadata = 2000,

    /// <summary>
    /// For deleting data.
    /// </summary>
    DeleteData = 2500,

    /// <summary>
    /// For notifying users.
    /// </summary>
    NotifyUser = 3000,

    /// <summary>
    /// For operations that should run last.
    /// </summary>
    RunLast = 10000,
}
