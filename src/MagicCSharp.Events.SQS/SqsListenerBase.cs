using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Events.SQS;

/// <summary>
///     Base class for SQS listener background services using long polling.
/// </summary>
/// <typeparam name="T">The type of message to consume.</typeparam>
public abstract class SqsListenerBase<T>(
    IServiceScopeFactory serviceScopeFactory,
    ILogger logger) : BackgroundService
{
    /// <summary>
    ///     The SQS queue URL to consume from.
    /// </summary>
    protected abstract string QueueUrl { get; }

    /// <summary>
    ///     Maximum number of messages to receive in one request (1-10).
    /// </summary>
    protected virtual int MaxNumberOfMessages => 10;

    /// <summary>
    ///     Long polling wait time in seconds (0-20).
    /// </summary>
    protected virtual int WaitTimeSeconds => 20;

    /// <summary>
    ///     Message visibility timeout in seconds.
    /// </summary>
    protected virtual int VisibilityTimeout => 30;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => Run(stoppingToken), stoppingToken);
    }

    private async Task Run(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting SQS listener on queue {QueueUrl}", QueueUrl);

        using var scope = serviceScopeFactory.CreateScope();
        var sqsClient = scope.ServiceProvider.GetRequiredService<IAmazonSQS>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogTrace("Fetching messages from SQS queue {QueueUrl}", QueueUrl);

                var receiveRequest = new ReceiveMessageRequest
                {
                    QueueUrl = QueueUrl,
                    MaxNumberOfMessages = MaxNumberOfMessages,
                    WaitTimeSeconds = WaitTimeSeconds,
                    VisibilityTimeout = VisibilityTimeout,
                };

                var response = await sqsClient.ReceiveMessageAsync(receiveRequest, stoppingToken);

                foreach (var message in response.Messages)
                {
                    try
                    {
                        var body = message.Body;
                        logger.LogTrace("Received message: {Message}", body);

                        var parsedMessage = ParseCallback(body, stoppingToken);
                        if (parsedMessage == null)
                        {
                            // Ignore this message, we don't know the type
                            // Still delete it so it doesn't get reprocessed
                            await DeleteMessage(sqsClient, message.ReceiptHandle, stoppingToken);
                            continue;
                        }

                        await OnMessage(parsedMessage, stoppingToken);

                        // Delete the message after successful processing
                        await DeleteMessage(sqsClient, message.ReceiptHandle, stoppingToken);
                    } catch (Exception e)
                    {
                        logger.LogError(e, "Failed to process SQS message");
                        // Message will become visible again after visibility timeout
                    }
                }
            } catch (TaskCanceledException)
            {
                // This is ok, happens on shutdown
                return;
            } catch (OperationCanceledException)
            {
                // This is ok, happens on shutdown
                return;
            } catch (Exception e)
            {
                logger.LogError(e, "Failed to get messages from SQS");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        // This is ok, happens on shutdown, we no longer receive messages
        logger.LogInformation("SQS listener on queue {QueueUrl} stopped", QueueUrl);
    }

    private async Task DeleteMessage(IAmazonSQS sqsClient, string receiptHandle, CancellationToken cancellationToken)
    {
        try
        {
            await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = QueueUrl,
                ReceiptHandle = receiptHandle,
            }, cancellationToken);
        } catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete message from SQS");
        }
    }

    /// <summary>
    ///     Parse the message body into the expected type.
    /// </summary>
    protected abstract T? ParseCallback(string body, CancellationToken cancellationToken);

    /// <summary>
    ///     Handle the parsed message.
    /// </summary>
    protected abstract Task OnMessage(T message, CancellationToken cancellationToken);
}