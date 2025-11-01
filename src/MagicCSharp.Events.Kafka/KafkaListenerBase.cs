using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.Kafka;

/// <summary>
/// Base class for Kafka listener background services.
/// </summary>
/// <typeparam name="T">The type of message to consume.</typeparam>
public abstract class KafkaListenerBase<T>(
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger) : BackgroundService
{
    /// <summary>
    /// The Kafka topic to subscribe to.
    /// </summary>
    protected abstract string Topic { get; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => Run(stoppingToken), stoppingToken);
    }

    private async Task Run(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Kafka listener on topic {Topic}", Topic);

        using var scope = serviceScopeFactory.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<IConsumer<Null, string>>();
        consumer.Subscribe(Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogTrace("Fetching messages from Kafka for topic {Topic}", Topic);
                var consumeResult = consumer.Consume(stoppingToken);

                try
                {
                    var body = consumeResult.Message.Value;
                    logger.LogTrace("Received message: {Message}", body);

                    var message = ParseCallback(body, stoppingToken) ?? throw new InvalidOperationException("Failed to parse message");
                    if (message == null)
                    {
                        // Ignore this event, we don't know the type
                        continue;
                    }

                    await OnMessage(message, stoppingToken);

                    // Commit the result to Kafka if we processed it successfully
                    // Auto commit is not a good idea, we should commit manually to ensure all messages are processed
                    consumer.Commit(consumeResult);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to process message");
                }
            }
            catch (TaskCanceledException)
            {
                // This is ok, happens on shutdown
                return;
            }
            catch (OperationCanceledException)
            {
                // This is ok, happens on shutdown
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to get messages from Kafka");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        // This is ok, happens on shutdown, we no longer receive messages
        logger.LogInformation("Kafka listener on topic {Topic} stopped", Topic);
    }

    /// <summary>
    /// Parse the message body into the expected type.
    /// </summary>
    protected abstract T? ParseCallback(string body, CancellationToken cancellationToken);

    /// <summary>
    /// Handle the parsed message.
    /// </summary>
    protected abstract Task OnMessage(T message, CancellationToken cancellationToken);
}
