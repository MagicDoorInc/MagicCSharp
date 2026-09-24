using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure;

namespace Acme.Leasing.Domains.Leases.Models.Entities;

/// <summary>The Lease as the rest of the application sees it.</summary>
public record Lease : LeaseEdit, IMagicEntity, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

/// <summary>The writable fields. Lease derives from this, so an entity is accepted wherever an edit is.</summary>
public record LeaseEdit
{
    public required long PropertyId { get; init; }
    public required string TenantName { get; init; }
    public required string TenantEmail { get; init; }
    public required decimal MonthlyRent { get; init; }
    public required decimal SecurityDeposit { get; init; }

    /// <summary>Calendar days in the property's time zone, not instants.</summary>
    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }
}

/// <summary>Query criteria. Every property is optional; a null one means "do not narrow on this".</summary>
public class LeaseFilter
{
    public List<long>? Ids { get; init; }
    public List<long>? PropertyIds { get; init; }
    public ComparableRange<DateTimeOffset>? Created { get; init; }
}
