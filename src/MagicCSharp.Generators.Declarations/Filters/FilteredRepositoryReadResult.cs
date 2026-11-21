namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>What <see cref="FilteredRepositoryReader" /> made of one class.</summary>
public sealed record FilteredRepositoryReadResult
{
    /// <summary>The repository, or null when the class gets no generated <c>ApplyFilter</c> (see <see cref="Diagnostics" />).</summary>
    public required FilteredRepositoryDeclaration? Declaration { get; init; }

    /// <summary>Why a partial repository that needs a generated <c>ApplyFilter</c> cannot get one.</summary>
    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }

    public static FilteredRepositoryReadResult NotARepository { get; } = new FilteredRepositoryReadResult
    {
        Declaration = null,
        Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
    };
}
