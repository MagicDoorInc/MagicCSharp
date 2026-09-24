using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure;

namespace Acme.Leasing.Domains.Charges.Models.Entities;

/// <summary>Something a tenant owes on a lease: rent, a deposit, a late fee.</summary>
public record Charge : ChargeEdit, IMagicEntity, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

/// <summary>The writable fields. Charge derives from this, so an entity is accepted wherever an edit is.</summary>
public record ChargeEdit
{
    public required long LeaseId { get; init; }
    public required ChargeType Type { get; init; }
    public required decimal Amount { get; init; }

    /// <summary>A calendar day in the property's time zone.</summary>
    public required DateOnly DueDate { get; init; }

    /// <summary>When the payment was recorded; null while the charge is outstanding.</summary>
    public DateTimeOffset? Paid { get; init; }

    /// <summary>For a late fee, the rent charge it was applied to. Null on every other charge.</summary>
    public long? LateFeeForChargeId { get; init; }
}

/// <summary>Query criteria. Every property is optional; a null one means "do not narrow on this".</summary>
public class ChargeFilter
{
    public List<long>? Ids { get; init; }
    public List<long>? LeaseIds { get; init; }
    public List<ChargeType>? Types { get; init; }
    public bool? IsPaid { get; init; }
    public ComparableRange<DateOnly>? DueDate { get; init; }
    public List<long>? LateFeeForChargeIds { get; init; }
    public ComparableRange<DateTimeOffset>? Created { get; init; }
}
