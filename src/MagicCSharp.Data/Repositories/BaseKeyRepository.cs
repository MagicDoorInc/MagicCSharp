using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MagicCSharp.Data.Dals;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure.Exceptions;

namespace MagicCSharp.Data.Repositories;

/// <summary>
/// Base repository implementation for entities that use string keys as primary identifiers.
/// </summary>
/// <typeparam name="TContext">The database context type.</typeparam>
/// <typeparam name="TDal">The data access layer type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TFilter">The filter type for queries.</typeparam>
/// <typeparam name="TEdit">The edit type for create/update operations.</typeparam>
public abstract class BaseKeyRepository<TContext, TDal, TEntity, TFilter, TEdit>(
    IDbContextFactory<TContext> contextFactory,
    IClock clock,
    ILogger logger) : IKeyRepository<TEntity, TFilter, TEdit>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalKey, IDal
    where TEntity : class, TEdit, IKeyEntity
    where TContext : DbContext
{
    public virtual async Task<int> Count(TFilter filter)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var query = GetQuery(context);
        query = ApplyFilter(query, filter);
        return await query.CountAsync();
    }

    public virtual async Task<IReadOnlyList<TEntity>> Get(TFilter filter)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var query = GetQuery(context);
        query = ApplyFilter(query, filter);
        query = ApplyOrder(query);
        var dals = await query.ToListAsync();
        return dals.Select(x => ToEntity(x)).ToList();
    }

    public virtual async Task<TEntity?> Get(string key)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var dal = await GetQuery(context).Where(x => x.Key == key).FirstOrDefaultAsync();
        if (dal == null)
        {
            return default(TEntity?);
        }

        return ToEntity(dal);
    }

    public async Task<IReadOnlyList<TEntity>> Get(IReadOnlyList<string> keys)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync();
        var dals = await GetQuery(context).Where(x => keys.Contains(x.Key)).ToListAsync();
        return dals.Select(x => ToEntity(x)).ToList();
    }

    public virtual async Task<TEntity> Create(TEdit edit)
    {
        logger.LogTrace("Create: edit={Edit}", edit);
        await using var context = await contextFactory.CreateDbContextAsync();
        var dal = CreateDal(edit);
        dal.Created = clock.Now().ToUniversalTime();
        dal.Updated = clock.Now().ToUniversalTime();
        AfterDalCreatedHook(dal, edit, context);
        GetDbSet(context).Add(dal);
        await context.SaveChangesAsync();
        return (await Get(dal.Key))!;
    }

    public virtual async Task<IReadOnlyList<string>> Create(IReadOnlyList<TEdit> edits)
    {
        logger.LogTrace("BatchCreate: edits={Edits}", edits);
        if (edits.Count == 0)
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync();

        var dals = new List<TDal>();
        foreach (var edit in edits)
        {
            var dal = CreateDal(edit);
            dal.Created = clock.Now().ToUniversalTime();
            dal.Updated = clock.Now().ToUniversalTime();
            AfterDalCreatedHook(dal, edit, context);
            dals.Add(dal);
        }

        GetDbSet(context).AddRange(dals);
        await context.SaveChangesAsync();
        return dals.Select(x => x.Key).ToList();
    }

    public virtual async Task<TEntity> Update(string key, TEdit edit)
    {
        logger.LogTrace("Update: key={Key}, edit={Edit}", key, edit);

        await using var context = await contextFactory.CreateDbContextAsync();
        var dal = await GetQuery(context).Where(x => x.Key == key).FirstOrDefaultAsync();
        if (dal == null)
        {
            throw GetNotFoundException(key);
        }

        dal.Apply(edit);
        AfterDalApplyHook(dal, edit, context);

        var hasChanges = context.ChangeTracker
            .Entries()
            .Any(e => e.State == EntityState.Modified || e.State == EntityState.Added ||
                      e.State == EntityState.Deleted);

        if (hasChanges)
        {
            dal.Updated = clock.Now().ToUniversalTime();
        }

        await context.SaveChangesAsync();
        return ToEntity(dal);
    }

    public Task<TEntity> Update(TEntity entity)
    {
        return Update(entity.Key, entity);
    }

    public virtual async Task Delete(string key)
    {
        logger.LogTrace("Delete: key={Key}", key);

        await using var context = await contextFactory.CreateDbContextAsync();
        var dal = await GetQuery(context).Where(x => x.Key == key).FirstOrDefaultAsync();
        if (dal == null)
        {
            throw GetNotFoundException(key);
        }

        AfterDalDeleteHook(dal, context);
        GetDbSet(context).Remove(dal);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Transform a DAL object to an entity.
    /// Override this to customize the transformation.
    /// </summary>
    protected virtual TEntity ToEntity(TDal dal)
    {
        return dal.ToEntity();
    }

    /// <summary>
    /// Hook called after creating a DAL object but before saving.
    /// Override this to add custom logic.
    /// </summary>
    protected virtual void AfterDalCreatedHook(TDal dal, TEdit edit, TContext context)
    {
        // Override in derived classes for custom logic after entity creation
    }

    /// <summary>
    /// Hook called after applying changes to a DAL object during update.
    /// Override this to add custom logic.
    /// </summary>
    protected virtual void AfterDalApplyHook(TDal dal, TEdit edit, TContext context)
    {
        // Override in derived classes for custom logic after entity updates
    }

    /// <summary>
    /// Hook called before deleting a DAL object.
    /// Override this to add custom logic.
    /// </summary>
    protected virtual void AfterDalDeleteHook(TDal dal, TContext context)
    {
        // Override in derived classes for custom logic before entity deletion
    }

    /// <summary>
    /// Apply default ordering to the query.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract IQueryable<TDal> ApplyOrder(IQueryable<TDal> query);

    /// <summary>
    /// Apply filtering to the query based on the filter object.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract IQueryable<TDal> ApplyFilter(IQueryable<TDal> query, TFilter filter);

    /// <summary>
    /// Get the DbSet for the DAL type.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract DbSet<TDal> GetDbSet(TContext context);

    /// <summary>
    /// Get the base query for this repository.
    /// Override this to add includes or other query modifications.
    /// </summary>
    protected virtual IQueryable<TDal> GetQuery(TContext context)
    {
        return GetDbSet(context);
    }

    /// <summary>
    /// Create a new DAL object from an edit object.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract TDal CreateDal(TEdit edit);

    /// <summary>
    /// Get the NotFoundException to throw when an entity is not found.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract NotFoundException GetNotFoundException(string key);
}
