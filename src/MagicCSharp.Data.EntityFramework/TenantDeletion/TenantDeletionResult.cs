namespace MagicCSharp.Data.EntityFramework.TenantDeletion;

/// <summary>What one tenant deletion removed: rows in total, and how many of its steps succeeded.</summary>
public sealed record TenantDeletionResult
{
    public required int TotalDeleted { get; init; }

    public required int SucceededCount { get; init; }

    public required int StepCount { get; init; }
}
