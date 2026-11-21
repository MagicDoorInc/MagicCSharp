using MagicCSharp.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.TenantDeletion;

/// <summary>
///     Deletes a tenant's rows: runs <see cref="TenantDeletionPlanner" />'s steps in order, and keeps going past a
///     step that fails.
///     <para>
///         A failing step is logged and skipped rather than ending the run: the other tables still lose the tenant's
///         rows, and the log names the one that did not. Running it again for the same tenant deletes nothing more and
///         fails nothing, so a redelivered tenant-deleted event is harmless.
///     </para>
/// </summary>
public sealed class TenantDataDestroyer(TenantDeletionPlanner planner, ILogger<TenantDataDestroyer> logger)
{
    /// <summary>Deletes every row <paramref name="tenantId" /> owns in the tables of <paramref name="destroyers" />.</summary>
    /// <param name="tenantId">The tenant being deleted.</param>
    /// <param name="destroyers">Every destroyer the application registered.</param>
    public async Task<TenantDeletionResult> DestroyTenant(long tenantId, IEnumerable<ITenantScopedDestroyer> destroyers)
    {
        var tenantDeletionPlan = planner.PlanDeletion(destroyers);

        var totalDeleted = 0;
        var succeededCount = 0;
        foreach (var destroyer in tenantDeletionPlan.Steps)
        {
            var deletedCount = await DestroyTenantInOneTableOrLogFailure(tenantId, destroyer);
            if (deletedCount != null)
            {
                totalDeleted += deletedCount.Value;
                succeededCount++;
            }
        }

        logger.LogInformation("Destroyed {TotalDeleted} rows through {SucceededCount}/{StepCount} destroyers for tenant {TenantId}",
            totalDeleted, succeededCount, tenantDeletionPlan.Steps.Count, tenantId);

        return new TenantDeletionResult
        {
            TotalDeleted = totalDeleted,
            SucceededCount = succeededCount,
            StepCount = tenantDeletionPlan.Steps.Count,
        };
    }

    /// <summary>The rows deleted, or null when the destroyer failed; the failure is logged.</summary>
    private async Task<int?> DestroyTenantInOneTableOrLogFailure(long tenantId, ITenantScopedDestroyer destroyer)
    {
        try
        {
            var deletedCount = await destroyer.DestroyTenant(tenantId);
            if (deletedCount > 0)
            {
                logger.LogTrace("Destroyed {DeletedCount} rows through {Destroyer} for tenant {TenantId}", deletedCount, destroyer.GetType().Name, tenantId);
            }

            return deletedCount;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to destroy data through {Destroyer} for tenant {TenantId}", destroyer.GetType().Name, tenantId);
            return null;
        }
    }
}
