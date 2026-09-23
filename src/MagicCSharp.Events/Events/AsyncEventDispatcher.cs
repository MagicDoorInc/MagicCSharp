using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using MagicCSharp.Events.Events.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.Events;

/// <summary>
///     Asynchronous event dispatcher that executes all registered handlers for an event.
///     Handlers are executed in priority order, with lower priority values executing first.
///     This dispatcher executes handlers immediately without queuing to external systems.
/// </summary>
public class AsyncEventDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    IEventTypeHolder eventTypeHolder,
    IEventsMetricsHandler metrics,
    ILogger<AsyncEventDispatcher> logger) : IAsyncEventDispatcher
{
    private static readonly ConcurrentDictionary<(Type handler, Type eventType), HandleDelegate?> _handleDelegateCache =
        [];

    public async Task Dispatch(MagicEvent magicEvent)
    {
        var eventType = magicEvent.GetType();
        metrics.GotEvent(eventType);

        // Create a scope for the event
        using var scope = serviceScopeFactory.CreateScope();

        // Set request id for the event
        using (
            logger.BeginScope(
                magicEvent.EventId[^12..])) // Use the last 12 characters of the event id as the request id
        {
            // Get pre-sorted handler types from EventTypeHolder (already sorted by Priority)
            var handlerTypes = eventTypeHolder.GetHandlerTypes(eventType);

            logger.LogTrace("Found {count} event handlers for {event}", handlerTypes.Count, eventType.Name);

            foreach (var handlerType in handlerTypes)
            {
                await ExecuteHandler(magicEvent, eventType, handlerType, scope);
            }
        }
    }

    private async Task ExecuteHandler(
        MagicEvent magicEvent,
        Type eventType,
        Type handlerType,
        IServiceScope scope)
    {
        // Get handler instance from ServiceProvider (registered as concrete type)
        var handler = scope.ServiceProvider.GetService(handlerType);
        if (handler == null)
        {
            logger.LogError("Handler {handler} not found in ServiceProvider", handlerType.Name);
            metrics.EventFailed(eventType, handlerType.Name,
                new InvalidOperationException($"Handler {handlerType.Name} not found in ServiceProvider"));
            return;
        }

        // Get compiled delegate for the handler method (zero runtime reflection)
        var handleDelegate = GetHandleDelegate(handlerType, eventType);
        if (handleDelegate == null)
        {
            logger.LogError("Handler {handler} does not have a Handle method, skipping", handlerType.Name);
            metrics.EventFailed(eventType, handlerType.Name,
                new InvalidOperationException($"Handler {handlerType.Name} does not have a Handle method"));
            return;
        }

        logger.LogTrace("Dispatching event {event} to handler {handler}", eventType.Name, handlerType.Name);

        using (logger.BeginScope(handlerType.Name)) // Set child request id for each handler
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await handleDelegate(handler, magicEvent);
            } catch (Exception handlerEx)
            {
                metrics.EventFailed(eventType, handlerType.Name, handlerEx);
                logger.LogError(handlerEx, "An error occurred while executing event handler {handler}",
                    handlerType.Name);
            }
            finally
            {
                stopwatch.Stop();
                logger.LogTrace("Event {event} handled by {handler} in {duration}ms", eventType.Name, handlerType.Name,
                    stopwatch.ElapsedMilliseconds);
                metrics.EventFinished(eventType, handlerType.Name, stopwatch.Elapsed);
            }
        }
    }

    /// <summary>
    ///     Gets a compiled delegate for the Handle method, eliminating runtime reflection.
    ///     Supports two cases:
    ///     1) Exact match: Handle(SpecificEvent)
    ///     2) Base-class parameter: Handle(MagicEvent) can accept SpecificEvent
    /// </summary>
    private static HandleDelegate? GetHandleDelegate(Type handlerType, Type eventType)
    {
        return _handleDelegateCache.GetOrAdd((handlerType, eventType), key =>
        {
            // Find the Handle method
            MethodInfo? method = null;

            // Fast exact lookup (public instance method)
            method = handlerType.GetMethod("Handle", [eventType]);
            if (method == null)
            {
                // Fallback: iterate instance public/non-public methods and allow base-class parameter matches
                foreach (var m in handlerType.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                                         BindingFlags.NonPublic))
                {
                    if (m.Name != "Handle")
                    {
                        continue;
                    }

                    var ps = m.GetParameters();
                    if (ps.Length == 1 && (ps[0].ParameterType == eventType ||
                                           ps[0].ParameterType.IsAssignableFrom(eventType)))
                    {
                        method = m;
                        break;
                    }
                }
            }

            if (method == null)
            {
                return null;
            }

            // Compile method to delegate using expression trees (zero runtime reflection after compilation)
            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var eventParam = Expression.Parameter(typeof(MagicEvent), "magicEvent");

            // Cast handler to concrete type
            var typedHandler = Expression.Convert(handlerParam, handlerType);

            // Cast event to method parameter type
            // The actual magicEvent is of eventType, and method parameter may be eventType or its base class (MagicEvent)
            var methodParamType = method.GetParameters()[0].ParameterType;
            Expression typedEvent = eventParam;
            if (methodParamType != typeof(MagicEvent))
            {
                // Method expects specific event type, cast from MagicEvent to that type
                // This is safe because the actual object is of eventType (which is methodParamType or its subclass)
                typedEvent = Expression.Convert(eventParam, methodParamType);
            }

            // Call the Handle method
            var call = Expression.Call(typedHandler, method, typedEvent);

            // Ensure return type is Task
            if (method.ReturnType != typeof(Task))
            {
                throw new InvalidOperationException(
                    $"Handler {handlerType.Name}.Handle method must return Task, but returns {method.ReturnType.Name}");
            }

            // Create lambda and compile to delegate
            return Expression.Lambda<HandleDelegate>(call, handlerParam, eventParam).Compile();
        });
    }

    // Delegate type: (handler instance, event instance) -> Task
    private delegate Task HandleDelegate(object handler, MagicEvent magicEvent);
}