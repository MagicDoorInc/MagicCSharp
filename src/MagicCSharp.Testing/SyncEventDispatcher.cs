using MagicCSharp.Events.Events;

namespace MagicCSharp.Testing;

/// <summary>
///     An <see cref="IEventDispatcher" /> that runs handlers inline and does not return until they finish, and
///     records everything it dispatched so a test can assert on it.
///     <para>
///         Production dispatch is fire-and-forget, which makes "the handler updated the projection" impossible to
///         assert without sleeping. Running handlers inline makes the assertion immediate and the test
///         deterministic. That is a deliberate difference from production, and the one thing to keep in mind
///         because of it: a handler that acquires a lock the emitting use case still holds would deadlock here
///         but not in production. Registering <see cref="InMemoryDistributedLockProvider" /> alongside this
///         dispatcher handles that — it lets the inline handler re-enter.
///     </para>
/// </summary>
public class SyncEventDispatcher(IAsyncEventDispatcher asyncDispatcher) : IEventDispatcher
{
    private readonly List<MagicEvent> dispatched = [];

    /// <summary>
    ///     Every event dispatched so far, in order.
    /// </summary>
    public IReadOnlyList<MagicEvent> DispatchedEvents
    {
        get
        {
            lock (dispatched)
            {
                return dispatched.ToList();
            }
        }
    }

    public void Dispatch(MagicEvent? magicEvent)
    {
        if (magicEvent is null)
        {
            return;
        }

        lock (dispatched)
        {
            dispatched.Add(magicEvent);
        }

        // Scope the lock re-entry window to exactly this dispatch, so handlers running inside the emitter's
        // lock can re-acquire it while genuinely concurrent flows elsewhere keep normal mutex semantics.
        InMemoryDistributedLockProvider.ReentrancyDepth.Value++;
        try
        {
            asyncDispatcher.Dispatch(magicEvent).GetAwaiter().GetResult();
        }
        finally
        {
            InMemoryDistributedLockProvider.ReentrancyDepth.Value--;
        }
    }

    /// <summary>
    ///     Forget everything dispatched so far, for a test that reuses the dispatcher across phases.
    /// </summary>
    public void ClearDispatchedEvents()
    {
        lock (dispatched)
        {
            dispatched.Clear();
        }
    }

    /// <summary>
    ///     Every dispatched event of this type, in order.
    /// </summary>
    public IReadOnlyList<T> GetDispatchedEvents<T>()
        where T : MagicEvent
    {
        return DispatchedEvents.OfType<T>().ToList();
    }

    /// <summary>
    ///     Whether any event of this type was dispatched.
    /// </summary>
    public bool HasDispatchedEvent<T>()
        where T : MagicEvent
    {
        return DispatchedEvents.OfType<T>().Any();
    }

    /// <summary>
    ///     Whether any event of this type matching the predicate was dispatched.
    /// </summary>
    public bool HasDispatchedEvent<T>(Func<T, bool> predicate)
        where T : MagicEvent
    {
        return DispatchedEvents.OfType<T>().Any(predicate);
    }
}
