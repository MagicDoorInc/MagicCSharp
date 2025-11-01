using System.Reflection;
using MagicCSharp.Events.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events;

/// <summary>
/// Extension methods for registering MagicCSharp Events services.
/// </summary>
public static class MagicEventsRegistrationExtensions
{
    /// <summary>
    /// Register core event infrastructure including event handlers, serialization, and metrics.
    /// Does NOT register IEventDispatcher - use RegisterLocalMagicEvents(), RegisterMagicKafkaEvents(), or RegisterMagicSQSEvents().
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="useOpenTelemetryMetrics">Use OpenTelemetry metrics instead of null metrics.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterMagicEvents(
        this IServiceCollection services,
        bool useOpenTelemetryMetrics = false)
    {
        var assembliesToScan = AppDomain.CurrentDomain.GetAssemblies();

        // Register event serialization
        var eventTypes = GetEventTypes(assembliesToScan);
        services.AddSingleton(new EventTypeHolder(eventTypes));
        services.AddSingleton<IEventSerializer>(new MagicEventSerializer(eventTypes));

        // Register metrics handler
        if (useOpenTelemetryMetrics)
        {
            services.AddSingleton<IEventsMetricsHandler, EventsMetricsHandler>();
        }
        else
        {
            services.AddSingleton<IEventsMetricsHandler, NullEventsMetricsHandler>();
        }

        // Register async event dispatcher
        services.AddSingleton<IAsyncEventDispatcher, AsyncEventDispatcher>();

        // Register event handlers
        RegisterEventHandlers(services, assembliesToScan);

        return services;
    }

    /// <summary>
    /// Register local event dispatcher for in-process event handling.
    /// This is perfect for local development, testing, and single-service applications.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="useOpenTelemetryMetrics">Use OpenTelemetry metrics instead of null metrics.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterLocalMagicEvents(
        this IServiceCollection services,
        bool useOpenTelemetryMetrics = false)
    {
        // Register local event dispatcher (wraps async with .Wait())
        services.AddSingleton<IEventDispatcher, LocalEventDispatcher>();

        return services;
    }

    private static IEnumerable<Type> GetEventTypes(Assembly[] assemblies)
    {
        var eventTypes = assemblies
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(MagicEvent)))
            .ToList();

        return eventTypes;
    }

    private static void RegisterEventHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var handlerCount = 0;

        var types = assemblies
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(type => !type.IsAbstract && !type.IsInterface);

        foreach (var type in types)
        {
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .ToList();

            foreach (var interfaceType in handlerInterfaces)
            {
                services.AddTransient(interfaceType, type);
                handlerCount++;
            }
        }
    }
}
