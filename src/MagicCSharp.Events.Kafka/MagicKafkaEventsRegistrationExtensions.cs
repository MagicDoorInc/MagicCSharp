using Confluent.Kafka;
using MagicCSharp.Events.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.Kafka;

/// <summary>
///     Extension methods for registering Kafka event dispatching services.
/// </summary>
public static class MagicKafkaEventsRegistrationExtensions
{
    /// <summary>
    ///     Register Kafka event dispatcher and background service.
    ///     This also calls RegisterMagicEvents() to register core infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Kafka configuration.</param>
    /// <param name="useOpenTelemetryMetrics">Use OpenTelemetry metrics instead of null metrics.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterMagicKafkaEvents(
        this IServiceCollection services,
        KafkaMagicEventConfiguration configuration,
        bool useOpenTelemetryMetrics = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Register core infrastructure
        services.RegisterMagicEvents(useOpenTelemetryMetrics);

        var host = configuration.BootstrapServers;
        var groupId = configuration.GroupId;
        var topic = configuration.Topic;

        // Register Kafka configuration
        services.AddSingleton(new KafkaEventsBackgroundServiceConfig(topic));

        // Register Kafka producer
        services.AddSingleton<IProducer<Null, string>>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<KafkaEventDispatcher>>();
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = host,
                BrokerAddressFamily = BrokerAddressFamily.V4,
            };

            var producerLogger = KafkaLoggerAdapter.GetProducerLogHandler<Null, string>(logger);
            return new ProducerBuilder<Null, string>(producerConfig).SetLogHandler(producerLogger).Build();
        });

        // Register Kafka consumer
        services.AddTransient<IConsumer<Null, string>>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<KafkaEventsBackgroundService>>();
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = host,
                BrokerAddressFamily = BrokerAddressFamily.V4,
                GroupId = groupId,
            };

            var consumerLogger = KafkaLoggerAdapter.GetConsumerLogHandler<Null, string>(logger);
            return new ConsumerBuilder<Null, string>(consumerConfig).SetLogHandler(consumerLogger).Build();
        });

        // Register Kafka event dispatcher
        services.AddSingleton<IEventDispatcher, KafkaEventDispatcher>();

        // Register background service
        services.AddHostedService<KafkaEventsBackgroundService>();

        return services;
    }
}