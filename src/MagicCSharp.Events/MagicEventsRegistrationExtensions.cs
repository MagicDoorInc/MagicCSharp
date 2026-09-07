using System.Reflection;
using MagicCSharp.Events.Events;
using MagicCSharp.Modules;
using MagicCSharp.Events.Events.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Events;

/// <summary>
///     Extension methods for registering MagicCSharp Events services.
/// </summary>
public static class MagicEventsRegistrationExtensions
{
    /// <summary>
    ///     Discovers event handlers and registers serialization, metrics and <c>IAsyncEventDispatcher</c>.
    ///     <para>
    ///         Does not register <c>IEventDispatcher</c> — that comes from a transport:
    ///         <c>AddLocalMagicEvents</c>, <c>AddMagicKafkaEvents</c> or <c>AddMagicSqsEvents</c>. Each of
    ///         those calls this for you, so you rarely need it directly.
    ///     </para>
    ///     <para>Idempotent: calling it twice does not register the handlers twice.</para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="useOpenTelemetryMetrics">Use OpenTelemetry metrics instead of null metrics.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMagicEvents(
        this IServiceCollection services,
        bool useOpenTelemetryMetrics = false)
    {
        // Idempotent, because each transport calls this and an application may also call it directly.
        // Without the guard every handler would be registered twice and each event handled twice.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IEventTypeHolder)))
        {
            return services;
        }

        // Step 1: Collect event types
        var eventTypes = new List<Type>();

        // Step 2: Collect handlers with priorities and register them to DI
        var handlersByEventType = new Dictionary<Type, List<(Type HandlerType, MagicEventPriority Priority)>>();
        var handlerCount = 0;

        // Single pass: process all types once
        foreach (var type in ApplicationTypes())
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
    ///     Register the in-process event dispatcher, for local development and single-service applications.
    ///     Handlers run on a background task, the same fire-and-forget shape Kafka and SQS give you, so switching
    ///     to either later is a registration change and nothing else.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="useOpenTelemetryMetrics">Use OpenTelemetry metrics instead of null metrics.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalMagicEvents(this IServiceCollection services, bool useOpenTelemetryMetrics = false)
    {
        // Self-sufficient on purpose. This used to register only IEventDispatcher, so calling it without
        // AddMagicEvents first left the application failing at resolution the first time it dispatched —
        // and nothing said so. AddMagicEvents is idempotent, so calling both is fine.
        services.AddMagicEvents(useOpenTelemetryMetrics);
        services.AddSingleton<IEventDispatcher, LocalEventDispatcher>();

        return services;
    }

    /// <inheritdoc cref="AddMagicEvents" />
    [Obsolete("Renamed to AddMagicEvents, for consistency with every other registration method.")]
    public static IServiceCollection RegisterMagicEvents(this IServiceCollection services, bool useOpenTelemetryMetrics = false)
    {
        return services.AddMagicEvents(useOpenTelemetryMetrics);
    }

    /// <inheritdoc cref="AddLocalMagicEvents" />
    [Obsolete("Renamed to AddLocalMagicEvents, for consistency with every other registration method.")]
    public static IServiceCollection RegisterLocalMagicEvents(this IServiceCollection services)
    {
        return services.AddLocalMagicEvents();
    }

    private static bool IsEventHandler(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        return type.GetGenericTypeDefinition() == typeof(IEventHandler<>);
    }

    /// <summary>
    ///     Every concrete type in the application. Goes through <see cref="ApplicationAssemblies" /> rather
    ///     than reading <see cref="AppDomain" /> directly: a domain project holding nothing but event
    ///     handlers is referenced by the host and touched by nothing, so .NET has not loaded it when this
    ///     runs, and its handlers used to go missing without a word.
    /// </summary>
    private static List<Type> ApplicationTypes()
    {
        return ApplicationAssemblies.All()
            .SelectMany(GetTypesSafely)
            .Where(type => !type.IsAbstract)
            .ToList();
    }

    /// <summary>
    ///     An assembly referencing a type it cannot load throws on <see cref="Assembly.GetTypes" /> and would abort
    ///     the whole scan. Keep the types that did load.
    /// </summary>
    private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null).Cast<Type>();
        }
    }
}