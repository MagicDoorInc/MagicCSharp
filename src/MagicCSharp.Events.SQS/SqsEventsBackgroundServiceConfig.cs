namespace MagicCSharp.Events.SQS;

/// <summary>
///     Configuration for the SQS events background service.
/// </summary>
public record SqsEventsBackgroundServiceConfig
{
    /// <summary>The SQS queue URL to consume from and send to.</summary>
    public required string QueueUrl { get; init; }

    /// <summary>Maximum number of messages to receive in one request (1-10). Default is 10.</summary>
    public int MaxNumberOfMessages { get; init; } = 10;

    /// <summary>Long polling wait time in seconds (0-20). Default is 20 for maximum efficiency.</summary>
    public int WaitTimeSeconds { get; init; } = 20;

    /// <summary>Message visibility timeout in seconds. Default is 30.</summary>
    public int VisibilityTimeout { get; init; } = 30;
}
