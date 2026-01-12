using System.Diagnostics;
using Confluent.Kafka;
using MagicCSharp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.Kafka;

/// <summary>
///     Base class for Kafka listener background services.
/// </summary>
/// <typeparam name="T">The type of message to consume.</typeparam>
public abstract class KafkaListenerBase<T>(
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger) : BackgroundService
{
    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FatalErrorRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NonFatalErrorRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan UnexpectedErrorRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     The Kafka topic to subscribe to.
    /// </summary>
    protected abstract string Topic { get; }

    /// <summary>
    ///     Parse the message body into the expected type.
    /// </summary>
    protected abstract T? ParseCallback(string body, CancellationToken cancellationToken);

    /// <summary>
    ///     Handle the parsed message.
    /// </summary>
    protected abstract Task OnMessage(T message, CancellationToken cancellationToken);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => Run(stoppingToken), stoppingToken);
    }

    private async Task Run(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();

        var requestIdHandler = scope.ServiceProvider.GetRequiredService<IRequestIdHandler>();

        // Create consumer and subscribe - if this fails, let exception propagate (startup failure)
        var consumer = scope.ServiceProvider.GetRequiredService<IConsumer<Null, string>>();
        consumer.Subscribe(Topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var _ = requestIdHandler.SetRequestId(); // Set request id for the message

                    // Use timeout-based Consume to:
                    // 1. Avoid blocking thread pool indefinitely
                    // 2. Periodically reset max.poll.interval.ms timer
                    // 3. Allow Kafka client library's automatic reconnection to work
                    var consumeResult = consumer.Consume(ConsumeTimeout);

                    if (consumeResult == null)
                    {
                        // Timeout - continue loop (this resets poll interval timer)
                        // Kafka client library handles automatic reconnection internally
                        continue;
                    }

                    // Process message
                    await ProcessMessage(consumeResult, consumer, stoppingToken);
                } catch (Exception ex)
                {
                    await HandleException(ex, stoppingToken);
                }
            }
        }
        finally
        {
            // Graceful shutdown: close consumer properly
            CloseConsumer(consumer);
        }
    }

    // Message processing
    private async Task ProcessMessage(
        ConsumeResult<Null, string> consumeResult,
        IConsumer<Null, string> consumer,
        CancellationToken stoppingToken)
    {
        var body = consumeResult.Message.Value;
        logger.LogTrace("Received message: {message}", body);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var message = ParseCallback(body, stoppingToken) ?? throw new Exception("Failed to parse message");

            await OnMessage(message, stoppingToken);

            // Only commit if processing succeeded
            consumer.Commit(consumeResult);

            logger.LogTrace("Processed message at partition={partition}, offset={offset} in {elapsed} ms",
                consumeResult.Partition.Value, consumeResult.Offset.Value, stopwatch.ElapsedMilliseconds);
        } catch (Exception ex)
        {
            // Processing failed - don't commit offset, message will be retried
            // All exceptions are logged and swallowed to continue processing other messages
            // If stoppingToken is cancelled, next loop iteration will check and exit gracefully
            logger.LogError(ex,
                "Failed to process message. Offset will not be committed. Message will be retried based on Kafka configuration: partition={partition}, offset={offset}",
                consumeResult.Partition.Value, consumeResult.Offset.Value);
        }
    }

    // Error handling
    private async Task HandleException(Exception ex, CancellationToken stoppingToken)
    {
        if (ex is OperationCanceledException)
        {
            // Normal shutdown - next loop iteration will check stoppingToken.IsCancellationRequested
            logger.LogInformation("Kafka consumer cancellation requested");
            return;
        }

        if (ex is ConsumeException consumeEx)
        {
            var error = consumeEx.Error;

            if (error.IsFatal)
            {
                logger.LogError(consumeEx,
                    "Fatal Kafka error: code={code}, reason={reason}. Consumer will attempt to continue after {delay} seconds.",
                    error.Code, error.Reason, FatalErrorRetryDelay.TotalSeconds);
                await Task.Delay(FatalErrorRetryDelay, stoppingToken);
            }
            else
            {
                // Non-fatal error (network issues, etc.) - Kafka library will auto-reconnect
                logger.LogWarning(consumeEx,
                    "Kafka consume error: code={code}, reason={reason}. Kafka client will attempt automatic recovery after {delay} seconds.",
                    error.Code, error.Reason, NonFatalErrorRetryDelay.TotalSeconds);
                await Task.Delay(NonFatalErrorRetryDelay, stoppingToken);
            }

            return;
        }

        if (ex is KafkaException kafkaEx)
        {
            logger.LogError(kafkaEx, "Kafka exception. Will attempt to continue after {delay} seconds.",
                FatalErrorRetryDelay.TotalSeconds);
            await Task.Delay(FatalErrorRetryDelay, stoppingToken);
            return;
        }

        // Unexpected error
        logger.LogError(ex, "Unexpected error in Kafka consumer loop. Will attempt to continue after {delay} seconds.",
            UnexpectedErrorRetryDelay.TotalSeconds);
        await Task.Delay(UnexpectedErrorRetryDelay, stoppingToken);
    }

    // Resource cleanup
    private void CloseConsumer(IConsumer<Null, string>? consumer)
    {
        if (consumer == null)
        {
            return;
        }

        try
        {
            consumer.Close();
            consumer.Dispose();
        } catch (Exception ex)
        {
            logger.LogError(ex, "Error closing Kafka consumer during shutdown");
        }

        logger.LogInformation("Kafka listener on topic {topic} stopped", Topic);
    }
}