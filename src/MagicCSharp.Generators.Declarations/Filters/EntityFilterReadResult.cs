namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>What <see cref="EntityFilterReader" /> made of one class or record.</summary>
public sealed record EntityFilterReadResult
{
    /// <summary>The filter, or null when it gets no generated <c>Matches</c> (see <see cref="Diagnostics" />).</summary>
    public required EntityFilterDeclaration? Declaration { get; init; }

    /// <summary>Why a <c>[GenerateMatches]</c> class cannot get a generated <c>Matches</c>.</summary>
    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }

    /// <summary>Nothing to write and nothing to report: the filter writes <c>Matches</c> itself, or its entity does not compile.</summary>
    public static EntityFilterReadResult NoMatchesToWrite { get; } = new EntityFilterReadResult
    {
        Declaration = null,
        Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
    };
}
