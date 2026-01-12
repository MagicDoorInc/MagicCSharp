using MagicCSharp.Events.Events;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Events.SQS;

/// <summary>
///     Extension methods for registering AWS SQS event dispatching services.
/// </summary>
public static class MagicSQSEventsRegistrationExtensions
{
    /// <summary>
    ///     Register SQS event dispatcher and background service.
    ///     This also calls RegisterMagicEvents() to register core infrastructure.
    ///     Note: IAmazonSQS client must be registered separately by the consumer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">SQS configuration.</param>
    /// <param name="useOpenTelemetryMetrics">Use OpenTelemetry metrics instead of null metrics.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterMagicSQSEvents(
        this IServiceCollection services,
        SqsMagicEventConfiguration configuration,
        bool useOpenTelemetryMetrics = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Register core infrastructure
        services.RegisterMagicEvents(useOpenTelemetryMetrics);

        // Validate parameters
        if (configuration.MaxNumberOfMessages < 1 || configuration.MaxNumberOfMessages > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration.MaxNumberOfMessages),
                "Must be between 1 and 10");
        }

        if (configuration.WaitTimeSeconds < 0 || configuration.WaitTimeSeconds > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration.WaitTimeSeconds), "Must be between 0 and 20");
        }

        if (configuration.VisibilityTimeout < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration.VisibilityTimeout), "Must be >= 0");
        }

        // Register SQS configuration
        services.AddSingleton(new SqsEventsBackgroundServiceConfig(configuration.QueueUrl,
            configuration.MaxNumberOfMessages, configuration.WaitTimeSeconds, configuration.VisibilityTimeout));

        // Register SQS event dispatcher
        services.AddSingleton<IEventDispatcher, SqsEventDispatcher>();

        // Register background service
        services.AddHostedService<SqsEventsBackgroundService>();

        return services;
    }
}