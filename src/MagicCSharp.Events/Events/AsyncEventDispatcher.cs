using System.Diagnostics;
using MagicCSharp.Events.Metrics;
using MagicCSharp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events;

/// <summary>
/// Asynchronous event dispatcher that executes all registered handlers for an event.
/// Handlers are executed in priority order, with lower priority values executing first.
/// This dispatcher executes handlers immediately without queuing to external systems.
/// </summary>
public class AsyncEventDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    IEventsMetricsHandler metrics,
    ILogger<AsyncEventDispatcher> logger) : IAsyncEventDispatcher
{
    public async Task Dispatch(MagicEvent magicEvent)
    {
        try
        {
            metrics.GotEvent(magicEvent.GetType());

            using var scope = serviceScopeFactory.CreateScope();

            var handlers = GetEventHandlers(magicEvent, scope);

            foreach (var handler in handlers)
            {
                await ExecuteHandler(magicEvent, handler);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while dispatching domain event");
        }
    }

    private async Task ExecuteHandler(MagicEvent magicEvent, object handler)
    {
        var eventType = magicEvent.GetType();
        try
        {
            var handlerType = handler.GetType();

            var handlerInterface = handlerType.GetInterfaces().FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

            if (handlerInterface == null || handlerInterface.GetGenericArguments()[0] != magicEvent.GetType())
            {
                logger.LogWarning("Handler {HandlerType} does not implement IEventHandler<{EventType}>",
                    handlerType.Name, eventType.Name);
                return;
            }

            logger.LogTrace("Dispatching event {EventType} to handler {HandlerType}",
                eventType.Name, handlerType.Name);

            var handleMethod = handlerInterface.GetMethod("Handle");
            if (handleMethod == null)
            {
                logger.LogWarning("Handler {HandlerType} does not have a Handle method, skipping", handlerType.Name);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var task = handleMethod.Invoke(handler, [magicEvent]);
                if (task is Task taskResult)
                {
                    await taskResult;
                }
                else
                {
                    logger.LogError("Handler {HandlerType} did not return a Task, skipping", handlerType.Name);
                }
            }
            catch (Exception handlerEx)
            {
                metrics.EventFailed(eventType, handlerType.Name, handlerEx);
                logger.LogError(handlerEx, "An error occurred while executing event handler {HandlerType}",
                    handlerType.Name);
            }
            finally
            {
                stopwatch.Stop();
                logger.LogTrace("Event {EventType} handled by {HandlerType} in {DurationMs}ms",
                    eventType.Name, handlerType.Name, stopwatch.ElapsedMilliseconds);
                metrics.EventFinished(eventType, handlerType.Name, stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while dispatching domain event");
        }
    }

    private IEnumerable<object> GetEventHandlers(MagicEvent magicEvent, IServiceScope scope)
    {
        var eventType = magicEvent.GetType();

        logger.LogTrace("Finding event handlers for {EventType}", eventType.Name);

        var interfaceType = typeof(IEventHandler<>).MakeGenericType(eventType);

        var handlers = scope.ServiceProvider.GetServices(interfaceType)
            .Where(x => x != null)
            .Select(x => x!)
            .DistinctBy(x => x.GetType().ToString())
            .ToList();

        logger.LogTrace("Found {Count} event handlers for {EventType}", handlers.Count, eventType.Name);

        return handlers.OrderBy(handler =>
        {
            var priorityProperty = handler.GetType().GetProperty("Priority");
            return priorityProperty != null ? (int)priorityProperty.GetValue(handler)! : int.MaxValue;
        });
    }
}
