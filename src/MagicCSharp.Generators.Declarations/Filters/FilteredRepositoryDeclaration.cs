using MagicCSharp.Generators.Declarations.Dals;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     A partial repository whose base declares <c>ApplyFilter(IQueryable&lt;TDal&gt;, TFilter)</c> abstract and that
///     does not write it: the types the query is written over, the filter's fields, and its custom filter method.
/// </summary>
public sealed record FilteredRepositoryDeclaration
{
    public required TypeDeclaration Repository { get; init; }

    public required TypeReference Dal { get; init; }

    /// <summary>The entity of the Dal's <c>IDalTransform&lt;TEntity, TEdit&gt;</c>, which the name rules match against.</summary>
    public required TypeReference Entity { get; init; }

    public required TypeReference Filter { get; init; }

    /// <summary>The abstract <c>ApplyFilter</c>'s accessibility as C# writes it (<c>protected</c>), which its override repeats.</summary>
    public required string ApplyFilterAccessibility { get; init; }

    public required EquatableArray<FilterField> FilterFields { get; init; }

    public required EquatableArray<MappableProperty> EntityProperties { get; init; }

    public required EquatableArray<MappableProperty> DalProperties { get; init; }

    public required CustomFilterMethod ApplyCustomFilter { get; init; }
}
