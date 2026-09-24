using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure;

namespace Acme.Leasing.Domains.Charges.LateFees.Models.Entities;

/// <summary>
///     How a property charges for late rent. One per property, so the property's id is the policy's id —
///     there is nothing to generate, and "the policy for this property" is a lookup by key.
/// </summary>
public record LateFeePolicy : LateFeePolicyEdit, IMagicEntity, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

/// <summary>The writable fields. LateFeePolicy derives from this, so an entity is accepted wherever an edit is.</summary>
public record LateFeePolicyEdit
{
    public required long PropertyId { get; init; }

    /// <summary>Days after the due date on which rent is still on time. Zero means late the next day.</summary>
    public required int GraceDays { get; init; }

    public required decimal Amount { get; init; }
}

/// <summary>Query criteria. Every property is optional; a null one means "do not narrow on this".</summary>
public class LateFeePolicyFilter
{
    public List<long>? Ids { get; init; }
    public ComparableRange<DateTimeOffset>? Created { get; init; }
}
