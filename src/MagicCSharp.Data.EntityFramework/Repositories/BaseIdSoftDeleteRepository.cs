using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     A paginated id-keyed repository whose rows are deleted by marking them.
///     <para>
///         The inherited hard <c>Delete</c> methods are still here and still remove the row. Both are offered on
///         purpose: soft delete for anything a user does, hard delete for a genuine purge. Decide per call site
///         which one you mean.
///     </para>
///     <para>
///         Nothing here hides deleted rows from reads. Exclude them in <c>ApplyFilter</c> so every caller gets
///         that for free rather than having to remember.
///     </para>
/// </summary>
public abstract class BaseIdSoftDeleteRepository<TContext, TDal, TEntity, TFilter, TEdit>(
    IDbContextFactory<TContext> contextFactory,
    IClock clock,
    ILoggerFactory loggerFactory)
    : BaseIdPaginatedRepository<TContext, TDal, TEntity, TFilter, TEdit>(contextFactory, clock, loggerFactory),
        ISoftDeleteRepository<TEntity, long, TFilter>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalId, IDal, IDalDeleted
    where TEntity : class, TEdit, IIdEntity, IDeletedEntity
    where TContext : DbContext
{
    public virtual async Task<TEntity> SoftDelete(long id)
    {
        Log.LogTrace("SoftDelete: id={Id}", id);

        await using var context = await ContextFactory.CreateDbContextAsync();

        var dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Id == id) ?? throw GetNotFoundException(id);

        var now = Clock.Now().ToUniversalTime();
        dal.Deleted = now;
        dal.Updated = now;
        AfterDalSoftDeleteHook(dal, context);

        await context.SaveChangesAsync();

        // Re-read for the same reason Update does: a hook that changed a foreign key clears the navigation
        // property that pointed through it.
        var savedId = dal.Id;
        dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Id == savedId);
        return dal?.ToEntity() ?? throw GetNotFoundException(savedId);
    }

    public virtual Task<TEntity> SoftDelete(TEntity entity)
    {
        return SoftDelete(entity.Id);
    }

    public virtual async Task<int> SoftDelete(IReadOnlyList<long> ids)
    {
        Log.LogTrace("SoftDelete: {Count} ids", ids.Count);

        if (ids.Count == 0)
        {
            return 0;
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var existing = await GetQuery(context).Where(x => ids.Contains(x.Id)).ToListAsync();

        var missing = ids.Except(existing.Select(x => x.Id)).ToList();
        if (missing.Count > 0)
        {
            throw GetNotFoundException(missing[0]);
        }

        MarkDeleted(existing, context);

        return await context.SaveChangesAsync();
    }

    public virtual async Task<int> SoftDelete(TFilter filter)
    {
        Log.LogTrace("SoftDelete by filter");

        await using var context = await ContextFactory.CreateDbContextAsync();

        var query = GetQuery(context);
        query = ApplyFilter(query, filter);
        var existing = await query.ToListAsync();

        if (existing.Count == 0)
        {
            return 0;
        }

        MarkDeleted(existing, context);

        return await context.SaveChangesAsync();
    }

    /// <summary>
    ///     Runs after a row is marked deleted but before it is saved. Use to cascade the mark to rows that only
    ///     make sense alongside this one.
    /// </summary>
    protected virtual void AfterDalSoftDeleteHook(TDal dal, TContext context)
    {
    }

    private void MarkDeleted(IReadOnlyList<TDal> dals, TContext context)
    {
        var now = Clock.Now().ToUniversalTime();

        foreach (var dal in dals)
        {
            dal.Deleted = now;
            dal.Updated = now;
            AfterDalSoftDeleteHook(dal, context);
        }
    }
}
