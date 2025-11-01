namespace MagicCSharp.Events;

/// <summary>
/// Interface for dispatching events asynchronously and waiting for handlers to complete.
/// </summary>
public interface IAsyncEventDispatcher
{
    /// <summary>
    /// Dispatch an event and wait for all handlers to complete.
    /// </summary>
    /// <param name="magicEvent">The event to dispatch.</param>
    Task Dispatch(MagicEvent magicEvent);
}
