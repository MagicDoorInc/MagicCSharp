using MagicCSharp.Generators.Declarations.Dals;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     A partial filter listing <c>IEntityFilter&lt;TEntity&gt;</c> without its own <c>Matches(TEntity)</c>: its fields,
///     the entity's properties, and its custom match method.
/// </summary>
public sealed record EntityFilterDeclaration
{
    public required TypeDeclaration Filter { get; init; }

    public required TypeReference Entity { get; init; }

    public required EquatableArray<FilterField> FilterFields { get; init; }

    public required EquatableArray<MappableProperty> EntityProperties { get; init; }

    public required CustomFilterMethod MatchesCustomFilter { get; init; }
}
