using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.Events;

/// <summary>
///     In-process event dispatcher. Handlers run on a background task; <see cref="Dispatch" /> returns as soon as
///     that task is started.
///     <para>
///         Fire-and-forget is deliberate: it is what <c>KafkaEventDispatcher</c> and <c>SqsEventDispatcher</c> do,
///         where dispatching means writing to a topic or queue and handlers run in a consumer somewhere else. A
///         local dispatcher that instead blocked until every handler finished would make the same use case behave
///         one way in development and another in production — a handler that re-enters a lock the emitter holds
///         would deadlock only after the switch to Kafka. Keeping both asynchronous is what makes swapping the
///         registration a configuration change rather than a rewrite.
///     </para>
///     <para>
///         In-flight dispatches are tracked and drained on process exit so a short-lived process does not lose
///         events it has already accepted. For tests that need handlers to have finished before the assertion,
///         use the synchronous dispatcher from <c>MagicCSharp.Testing</c> instead of blocking here.
///     </para>
/// </summary>
public class LocalEventDispatcher : IEventDispatcher
{
    private readonly IAsyncEventDispatcher asyncEventDispatcher;
    private readonly List<Task> inFlight = [];
    private readonly ILogger<LocalEventDispatcher> logger;

    public LocalEventDispatcher(IAsyncEventDispatcher asyncEventDispatcher, ILogger<LocalEventDispatcher> logger)
    {
        this.asyncEventDispatcher = asyncEventDispatcher;
        this.logger = logger;

        AppDomain.CurrentDomain.ProcessExit += (_, _) => DrainInFlight();
    }

    public void Dispatch(MagicEvent? magicEvent)
    {
        if (magicEvent is null)
        {
            return;
        }

        logger.LogTrace("Dispatching event {EventType} ({EventId})", magicEvent.GetType().Name, magicEvent.EventId);

        Track(asyncEventDispatcher.Dispatch(magicEvent));
    }

    private void Track(Task task)
    {
        lock (inFlight)
        {
            inFlight.Add(task);
        }

        // Remove on completion so a long-lived process does not accumulate finished tasks. The dispatcher
        // itself never faults — AsyncEventDispatcher catches and reports per-handler failures — so there is
        // no exception to observe here.
        _ = task.ContinueWith(completed =>
        {
            lock (inFlight)
            {
                inFlight.Remove(completed);
            }
        }, TaskScheduler.Default);
    }

    private void DrainInFlight()
    {
        Task[] pending;
        lock (inFlight)
        {
            pending = inFlight.ToArray();
        }

        if (pending.Length == 0)
        {
            return;
        }

        logger.LogInformation("Waiting for {Count} in-flight event dispatches to complete", pending.Length);

        try
        {
            Task.WaitAll(pending);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An in-flight event dispatch failed during shutdown");
        }
    }
}
