namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>One line of a generated mapping: <c>TargetName = source.SourceName</c>.</summary>
public sealed record PropertyAssignment
{
    public required string TargetName { get; init; }

    public required string SourceName { get; init; }
}
