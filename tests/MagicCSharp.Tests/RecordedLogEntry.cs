using Microsoft.Extensions.Logging;

namespace MagicCSharp.Tests;

/// <summary>One line a test's logger received: its category, level and formatted message.</summary>
public record RecordedLogEntry
{
    public required string Category { get; init; }

    public required LogLevel Level { get; init; }

    public required string Message { get; init; }
}
