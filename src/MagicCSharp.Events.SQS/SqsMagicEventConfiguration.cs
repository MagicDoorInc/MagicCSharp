namespace MagicCSharp.Events.SQS;

/// <summary>
/// Configuration for SQS event dispatching and consuming.
/// </summary>
/// <param name="QueueUrl">SQS queue URL.</param>
/// <param name="MaxNumberOfMessages">Maximum number of messages to receive per request (1-10). Default is 10.</param>
/// <param name="WaitTimeSeconds">Long polling wait time in seconds (0-20). Default is 20.</param>
/// <param name="VisibilityTimeout">Message visibility timeout in seconds. Default is 30.</param>
public record SqsMagicEventConfiguration(
    string QueueUrl,
    int MaxNumberOfMessages = 10,
    int WaitTimeSeconds = 20,
    int VisibilityTimeout = 30);
