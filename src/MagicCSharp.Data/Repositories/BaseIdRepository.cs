using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MagicCSharp.Data.Dals;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure.Exceptions;

namespace MagicCSharp.Data.Repositories;

/// <summary>
/// Base repository implementation for entities with numeric ID identifiers.
/// </summary>
/// <typeparam name="TContext">The database context type.</typeparam>
/// <typeparam name="TDal">The data access layer type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TFilter">The filter type for queries.</typeparam>
/// <typeparam name="TEdit">The edit type for create/update operations.</typeparam>
public abstract class BaseIdRepository<TContext, TDal, TEntity, TFilter, TEdit>(
    IDbContextFactory<TContext> contextFactory,
    IClock clock,
    ILogger logger) : IRepository<TEntity, TFilter, TEdit>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalId, IDal
    where TEntity : class, TEdit, IIdEntity
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

    public async Task<IReadOnlyList<TEntity>> Get(IReadOnlyList<long> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync();
        var dals = await GetQuery(context).Where(x => ids.Contains(x.Id)).ToListAsync();
        return dals.Select(x => ToEntity(x)).ToList();
    }

    public virtual async Task<TEntity?> Get(long id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var dal = await GetQuery(context).Where(x => x.Id == id).FirstOrDefaultAsync();
        if (dal == null)
        {
            return default(TEntity?);
        }

        return ToEntity(dal);
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
        return (await Get(dal.Id))!;
    }

    public virtual async Task<IReadOnlyList<long>> Create(IReadOnlyList<TEdit> edits)
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
        return dals.Select(x => x.Id).ToList();
    }

    public virtual async Task<TEntity> Update(long id, TEdit edit)
    {
        logger.LogTrace("Update: id={Id}, edit={Edit}", id, edit);

        await using var context = await contextFactory.CreateDbContextAsync();
        var dal = await GetQuery(context).Where(x => x.Id == id).FirstOrDefaultAsync();
        if (dal == null)
        {
            throw GetNotFoundException(id);
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
        return Update(entity.Id, entity);
    }

    public virtual async Task Delete(long id)
    {
        logger.LogTrace("Delete: id={Id}", id);

        await using var context = await contextFactory.CreateDbContextAsync();
        var dal = await GetQuery(context).Where(x => x.Id == id).FirstOrDefaultAsync();
        if (dal == null)
        {
            throw GetNotFoundException(id);
        }

        GetDbSet(context).Remove(dal);
        AfterDalDeleteHook(dal, context);

        await context.SaveChangesAsync();
    }

    public async Task Delete(IReadOnlyList<long> ids)
    {
        logger.LogTrace("Delete: ids={Ids}", ids);

        if (ids.Count == 0)
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync();

        // Retrieve all messages with ids in the provided list
        var dals = await GetQuery(context).Where(m => ids.Contains(m.Id)).ToListAsync();

        // Remove these messages from the database
        GetDbSet(context).RemoveRange(dals);

        foreach (var dal in dals)
        {
            AfterDalDeleteHook(dal, context);
        }

        // Save the changes to the database
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Hook called after applying changes to a DAL object during update.
    /// Override this to add custom logic.
    /// </summary>
    protected virtual void AfterDalApplyHook(TDal dal, TEdit edit, TContext context)
    {
    }

    /// <summary>
    /// Hook called before deleting a DAL object.
    /// Override this to add custom logic.
    /// </summary>
    protected virtual void AfterDalDeleteHook(TDal dal, TContext context)
    {
    }

    /// <summary>
    /// Hook called after creating a DAL object but before saving.
    /// Override this to add custom logic.
    /// </summary>
    protected virtual void AfterDalCreatedHook(TDal dal, TEdit edit, TContext context)
    {
    }

    /// <summary>
    /// Apply default ordering to the query (by ID descending).
    /// Override this to change the default ordering.
    /// </summary>
    protected virtual IQueryable<TDal> ApplyOrder(IQueryable<TDal> query)
    {
        return query.OrderByDescending(x => x.Id);
    }

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
    protected abstract NotFoundException GetNotFoundException(long id);

    /// <summary>
    /// Transform a DAL object to an entity.
    /// Override this to customize the transformation.
    /// </summary>
    protected virtual TEntity ToEntity(TDal dal)
    {
        return dal.ToEntity();
    }
}
