using System.Diagnostics;
using System.Reflection;
using MagicCSharp.Events.Metrics;
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
        var handlerType = handler.GetType();

        var handleMethod = GetHandleMethod(handlerType, eventType);
        if (handleMethod == null)
        {
            logger.LogWarning("Handler {HandlerType} does not have a Handle method, skipping", handlerType.Name);
            return;
        }

        try
        {
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
                    logger.LogWarning("Handler {HandlerType} did not return a Task, skipping", handlerType.Name);
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

    private static MethodInfo? GetHandleMethod(Type handlerType, Type eventType)
    {
        foreach (var method in handlerType.GetMethods())
        {
            if (method.Name != "Handle")
            {
                continue;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == eventType)
            {
                return method;
            }
        }

        return null;
    }
}
