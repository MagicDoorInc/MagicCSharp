using MagicCSharp.Data.Repositories;

namespace MagicCSharp.Data.EntityFramework.TenantDeletion;

/// <summary>What deleting a tenant runs, in order, and which tables it leaves alone.</summary>
public sealed record TenantDeletionPlan
{
    /// <summary>The destroyers to run, children before the parents their foreign keys point at.</summary>
    public required IReadOnlyList<ITenantScopedDestroyer> Steps { get; init; }

    /// <summary>The destroyers left out because their table is <c>[KeptOnTenantDeletion]</c>.</summary>
    public required IReadOnlyList<KeptTenantTable> Kept { get; init; }
}
