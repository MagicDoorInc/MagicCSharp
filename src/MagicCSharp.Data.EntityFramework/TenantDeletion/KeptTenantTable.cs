using MagicCSharp.Data.Repositories;

namespace MagicCSharp.Data.EntityFramework.TenantDeletion;

/// <summary>A destroyer tenant deletion leaves out, the table it would have emptied, and why the table stays.</summary>
public sealed record KeptTenantTable
{
    public required ITenantScopedDestroyer Destroyer { get; init; }

    public required Type DalType { get; init; }

    public required string Reason { get; init; }
}
