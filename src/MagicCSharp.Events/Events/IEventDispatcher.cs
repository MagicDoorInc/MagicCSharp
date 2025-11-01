namespace MagicCSharp.Events;

/// <summary>
/// Interface for dispatching events synchronously (non-blocking).
/// </summary>
public interface IEventDispatcher
{
    /// <summary>
    /// Dispatch an event if it is not null.
    /// This method is typically non-blocking and queues the event for processing.
    /// </summary>
    /// <param name="magicEvent">The event to dispatch.</param>
    void Dispatch(MagicEvent? magicEvent);
}
