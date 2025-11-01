namespace MagicCSharp.Events.SQS;

/// <summary>
/// Configuration for the SQS events background service.
/// </summary>
/// <param name="QueueUrl">The SQS queue URL to consume from and send to.</param>
/// <param name="MaxNumberOfMessages">Maximum number of messages to receive in one request (1-10). Default is 10.</param>
/// <param name="WaitTimeSeconds">Long polling wait time in seconds (0-20). Default is 20 for maximum efficiency.</param>
/// <param name="VisibilityTimeout">Message visibility timeout in seconds. Default is 30.</param>
public record SqsEventsBackgroundServiceConfig(
    string QueueUrl,
    int MaxNumberOfMessages = 10,
    int WaitTimeSeconds = 20,
    int VisibilityTimeout = 30);
