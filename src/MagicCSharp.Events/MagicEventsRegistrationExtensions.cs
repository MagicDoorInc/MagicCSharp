using System.Reflection;
using MagicCSharp.Events.Events;
using MagicCSharp.Events.Events.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Events;

/// <summary>
///     Extension methods for registering MagicCSharp Events services.
/// </summary>
public static class MagicEventsRegistrationExtensions
{
    /// <summary>
    ///     Register core event infrastructure including event handlers, serialization, and metrics.
    ///     Does NOT register IEventDispatcher - use RegisterLocalMagicEvents(), RegisterMagicKafkaEvents(), or
    ///     RegisterMagicSQSEvents().
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="useOpenTelemetryMetrics">Use OpenTelemetry metrics instead of null metrics.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterMagicEvents(
        this IServiceCollection services,
        bool useOpenTelemetryMetrics = false)
    {
        // Step 1: Collect event types
        var eventTypes = new List<Type>();

        // Step 2: Collect handlers with priorities and register them to DI
        var handlersByEventType = new Dictionary<Type, List<(Type HandlerType, MagicEventPriority Priority)>>();
        var handlerCount = 0;

        // Single pass: process all types once
        foreach (var type in LoadAllAppDomainTypes())
        {
            // Collect event types
            if (type.IsSubclassOf(typeof(MagicEvent)))
            {
                eventTypes.Add(type);
            }

            // Process handlers: collect for EventTypeHolder and register to DI
            var handlerInterfaces = type.GetInterfaces().Where(IsEventHandler).ToList();
            if (handlerInterfaces.Count == 0)
            {
                continue;
            }

            // Register handler type directly to DI (simpler, and we get instances by type)
            services.AddTransient(type);
            handlerCount += handlerInterfaces.Count;

            // Collect handler info for EventTypeHolder (one handler can handle multiple event types)
            foreach (var interfaceType in handlerInterfaces)
            {
                var eventType = interfaceType.GetGenericArguments()[0];

                // Get Priority from static property (no instance needed!)
                var priorityProperty = type.GetProperty("Priority",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                var priority = priorityProperty != null
                    ? (MagicEventPriority)priorityProperty.GetValue(null)!
                    : MagicEventPriority.AddDataNoDependencies;

                if (!handlersByEventType.TryGetValue(eventType, out var handlers))
                {
                    handlers = [];
                    handlersByEventType[eventType] = handlers;
                }

                handlers.Add((type, priority));
            }
        }

        // Step 3: Sort handlers by priority for each event type
        var sortedHandlersByEventType = handlersByEventType.ToDictionary(kvp => kvp.Key,
            kvp => (IReadOnlyList<Type>)kvp.Value
                .OrderBy(h => h.Priority)
                .Select(h => h.HandlerType)
                .ToList()
                .AsReadOnly());

        var readonlyEventTypes = eventTypes.AsReadOnly();

        // Register EventTypeHolder and EventSerializer
        services.AddSingleton<IEventTypeHolder>(new MagicEventTypeHolder(readonlyEventTypes,
            sortedHandlersByEventType));
        services.AddSingleton<IEventSerializer>(new MagicEventSerializer(readonlyEventTypes));

        // Register async event dispatcher
        services.AddSingleton<IAsyncEventDispatcher, AsyncEventDispatcher>();

        // Register metrics handler
        if (useOpenTelemetryMetrics)
        {
            services.AddSingleton<IEventsMetricsHandler, EventsMetricsHandler>();
        }
        else
        {
            services.AddSingleton<IEventsMetricsHandler, NullEventsMetricsHandler>();
        }

        return services;
    }

    /// <summary>
    ///     Register local event dispatcher for in-process event handling.
    ///     This is perfect for local development, testing, and single-service applications.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterLocalMagicEvents(this IServiceCollection services)
    {
        // Register local event dispatcher (wraps async with .Wait())
        services.AddSingleton<IEventDispatcher, LocalEventDispatcher>();

        return services;
    }

    private static bool IsEventHandler(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        return type.GetGenericTypeDefinition() == typeof(IEventHandler<>);
    }

    private static List<Type> LoadAllAppDomainTypes()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract)
            .ToList();
    }
}