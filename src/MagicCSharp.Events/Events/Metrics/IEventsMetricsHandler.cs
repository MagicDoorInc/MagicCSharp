using System.Diagnostics.Metrics;

namespace MagicCSharp.Events.Events.Metrics;

/// <summary>
///     Interface for tracking event processing metrics.
/// </summary>
public interface IEventsMetricsHandler
{
    /// <summary>
    ///     Record that an event was received.
    /// </summary>
    void GotEvent(Type eventType);

    /// <summary>
    ///     Record that an event handler failed.
    /// </summary>
    void EventFailed(Type eventType, string handlerName, Exception exception);

    /// <summary>
    ///     Record that an event handler finished successfully.
    /// </summary>
    void EventFinished(Type eventType, string handlerName, TimeSpan executionTime);
}

/// <summary>
///     Null implementation of IEventsMetricsHandler that does nothing.
/// </summary>
public class NullEventsMetricsHandler : IEventsMetricsHandler
{
    public void GotEvent(Type eventType)
    {
    }

    public void EventFailed(Type eventType, string handlerName, Exception exception)
    {
    }

    public void EventFinished(Type eventType, string handlerName, TimeSpan executionTime)
    {
    }
}

/// <summary>
///     OpenTelemetry-based implementation of IEventsMetricsHandler.
/// </summary>
public class EventsMetricsHandler(IMeterFactory meterFactory) : IEventsMetricsHandler
{
    private readonly Counter<int> eventFailedCounter =
        meterFactory.Create("MagicCSharp.Events").CreateCounter<int>("Events.Failed");

    private readonly Counter<int> eventFinishedCounter =
        meterFactory.Create("MagicCSharp.Events").CreateCounter<int>("Events.Finished");

    private readonly Histogram<double> executionTimeHistogram =
        meterFactory.Create("MagicCSharp.Events").CreateHistogram<double>("Events.ExecutionTime");

    private readonly Counter<int> gotEventCounter =
        meterFactory.Create("MagicCSharp.Events").CreateCounter<int>("Events");

    private readonly Meter meter = meterFactory.Create("MagicCSharp.Events");

    public void GotEvent(Type eventType)
    {
        gotEventCounter.Add(1, new KeyValuePair<string, object?>("eventType", eventType.Name));
    }

    public void EventFailed(Type eventType, string handlerName, Exception exception)
    {
        eventFailedCounter.Add(1, new KeyValuePair<string, object?>("eventType", eventType.Name),
            new KeyValuePair<string, object?>("handlerName", handlerName));
    }

    public void EventFinished(Type eventType, string handlerName, TimeSpan executionTime)
    {
        eventFinishedCounter.Add(1, new KeyValuePair<string, object?>("eventType", eventType.Name),
            new KeyValuePair<string, object?>("handlerName", handlerName),
            new KeyValuePair<string, object?>("executionTime", executionTime.TotalMilliseconds));

        executionTimeHistogram.Record(executionTime.TotalMilliseconds,
            new KeyValuePair<string, object?>("eventType", eventType.Name),
            new KeyValuePair<string, object?>("handlerName", handlerName));
    }
}