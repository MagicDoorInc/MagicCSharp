namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>What <see cref="FilteredRepositoryReader" /> made of one class.</summary>
public sealed record FilteredRepositoryReadResult
{
    /// <summary>The repository, or null when the class gets no generated <c>ApplyFilter</c> (see <see cref="Diagnostics" />).</summary>
    public required FilteredRepositoryDeclaration? Declaration { get; init; }

    /// <summary>Why a <c>[GenerateFilterQuery]</c> class cannot get a generated <c>ApplyFilter</c>.</summary>
    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }

    /// <summary>Nothing to write and nothing to report: the repository writes <c>ApplyFilter</c> itself, or a type it names does not compile.</summary>
    public static FilteredRepositoryReadResult NoApplyFilterToWrite { get; } = new FilteredRepositoryReadResult
    {
        Declaration = null,
        Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
    };
}
