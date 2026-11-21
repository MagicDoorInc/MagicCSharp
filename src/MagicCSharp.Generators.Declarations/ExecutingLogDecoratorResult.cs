namespace MagicCSharp.Generators.Declarations;

/// <summary>What a <c>[LogExecuting]</c> class gets: its decorator file, or the reasons it gets none.</summary>
public sealed record ExecutingLogDecoratorResult
{
    /// <summary>The decorator; null when the class is refused.</summary>
    public required GeneratedFile? File { get; init; }

    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }
}
