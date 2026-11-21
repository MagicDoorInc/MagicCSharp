namespace MagicCSharp.Clients.Tests;

/// <summary>One request a <see cref="RecordingHttpMessageHandler" /> received.</summary>
public record RecordedRequest
{
    public required string Method { get; init; }

    public required string Host { get; init; }

    public required string PathAndQuery { get; init; }

    /// <summary>The body as sent; null when the request had none.</summary>
    public required string? Body { get; init; }
}
