namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>What <see cref="EntityFilterReader" /> made of one class or record.</summary>
public sealed record EntityFilterReadResult
{
    /// <summary>The filter, or null when it gets no generated <c>Matches</c> (see <see cref="Diagnostics" />).</summary>
    public required EntityFilterDeclaration? Declaration { get; init; }

    /// <summary>Why a partial filter that needs a generated <c>Matches</c> cannot get one.</summary>
    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }

    public static EntityFilterReadResult NotAFilter { get; } = new EntityFilterReadResult
    {
        Declaration = null,
        Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
    };
}
