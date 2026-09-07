using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     A paginated key-keyed repository whose rows are deleted by marking them. See
///     <see cref="BaseIdSoftDeleteRepository{TContext,TDal,TEntity,TFilter,TEdit}" /> for how soft and hard delete
///     sit alongside each other.
/// </summary>
public abstract class BaseKeySoftDeleteRepository<TContext, TDal, TEntity, TFilter, TEdit>(
    IDbContextFactory<TContext> contextFactory,
    IClock clock,
    ILoggerFactory loggerFactory)
    : BaseKeyPaginatedRepository<TContext, TDal, TEntity, TFilter, TEdit>(contextFactory, clock, loggerFactory),
        ISoftDeleteRepository<TEntity, string, TFilter>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalKey, IDal, IDalDeleted
    where TEntity : class, TEdit, IKeyEntity, IDeletedEntity
    where TContext : DbContext
{
    public virtual async Task<TEntity> SoftDelete(string key)
    {
        Log.LogTrace("SoftDelete: key={Key}", key);

        await using var context = await ContextFactory.CreateDbContextAsync();

        var dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Key == key) ?? throw GetNotFoundException(key);

        var now = Clock.Now().ToUniversalTime();
        dal.Deleted = now;
        dal.Updated = now;
        AfterDalSoftDeleteHook(dal, context);

        await context.SaveChangesAsync();

        var savedKey = dal.Key;
        dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Key == savedKey);
        return dal?.ToEntity() ?? throw GetNotFoundException(savedKey);
    }

    public virtual Task<TEntity> SoftDelete(TEntity entity)
    {
        return SoftDelete(entity.Key);
    }

    public virtual async Task<int> SoftDelete(IReadOnlyList<string> keys)
    {
        Log.LogTrace("SoftDelete: {Count} keys", keys.Count);

        if (keys.Count == 0)
        {
            return 0;
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var existing = await GetQuery(context).Where(x => keys.Contains(x.Key)).ToListAsync();

        var missing = keys.Except(existing.Select(x => x.Key)).ToList();
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

    /// <inheritdoc cref="BaseIdSoftDeleteRepository{TContext,TDal,TEntity,TFilter,TEdit}.AfterDalSoftDeleteHook" />
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
