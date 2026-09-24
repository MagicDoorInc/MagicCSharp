using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>
///     A stand-in entity, deliberately boring, with one field of each kind the repository has to translate:
///     text to filter by, a number to compare, an enum stored by name, and a nullable.
/// </summary>
public record Widget : WidgetEdit, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record WidgetEdit
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required int Quantity { get; init; }
    public required WidgetStatus Status { get; init; }
}

public enum WidgetStatus
{
    Draft,
    Active,
    Retired,
}

/// <summary>A null property means "do not narrow on this".</summary>
public class WidgetFilter
{
    public List<long>? Ids { get; init; }
    public string? Name { get; init; }
    public string? DescriptionContains { get; init; }
    public WidgetStatus? Status { get; init; }
    public int? MinimumQuantity { get; init; }
}
