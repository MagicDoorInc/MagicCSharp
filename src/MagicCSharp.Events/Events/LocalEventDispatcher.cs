using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.Events;

/// <summary>
///     Synchronous event dispatcher that wraps IAsyncEventDispatcher for local, blocking event execution.
///     This dispatcher blocks the calling thread until all handlers complete.
///     For async execution, use IAsyncEventDispatcher directly.
/// </summary>
public class LocalEventDispatcher(
    IAsyncEventDispatcher asyncEventDispatcher,
    ILogger<LocalEventDispatcher> logger) : IEventDispatcher
{
    public void Dispatch(MagicEvent? magicEvent)
    {
        if (magicEvent is null)
        {
            return;
        }

        logger.LogTrace("Dispatching event {EventType} synchronously", magicEvent.GetType().Name);

        try
        {
            // Block until all handlers complete
            asyncEventDispatcher.Dispatch(magicEvent).Wait();
        } catch (AggregateException ex)
        {
            // Unwrap AggregateException from .Wait()
            logger.LogError(ex.InnerException ?? ex, "Error dispatching event synchronously");
            throw ex.InnerException ?? ex;
        }
    }
}