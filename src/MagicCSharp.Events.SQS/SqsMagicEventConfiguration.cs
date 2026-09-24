namespace MagicCSharp.Events.SQS;

/// <summary>
///     Configuration for SQS event dispatching and consuming.
/// </summary>
public record SqsMagicEventConfiguration
{
    /// <summary>SQS queue URL.</summary>
    public required string QueueUrl { get; init; }

    /// <summary>Maximum number of messages to receive per request (1-10). Default is 10.</summary>
    public int MaxNumberOfMessages { get; init; } = 10;

    /// <summary>Long polling wait time in seconds (0-20). Default is 20.</summary>
    public int WaitTimeSeconds { get; init; } = 20;

    /// <summary>Message visibility timeout in seconds. Default is 30.</summary>
    public int VisibilityTimeout { get; init; } = 30;
}
