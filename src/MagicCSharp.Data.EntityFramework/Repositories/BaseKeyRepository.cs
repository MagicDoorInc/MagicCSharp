using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     Entity Framework implementation of <see cref="IRepository{TEntity,TKey,TEdit,TFilter}" /> for entities keyed
///     by an unguessable string key. Identical in behaviour to
///     <see cref="BaseIdRepository{TContext,TDal,TEntity,TFilter,TEdit}" />; see that class for how the pieces fit
///     together.
/// </summary>
/// <typeparam name="TContext">The database context type.</typeparam>
/// <typeparam name="TDal">The row type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TFilter">The query filter type.</typeparam>
/// <typeparam name="TEdit">The type carrying the writable fields.</typeparam>
public abstract class BaseKeyRepository<TContext, TDal, TEntity, TFilter, TEdit> : IRepository<TEntity, string, TEdit, TFilter>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalKey, IDal
    where TEntity : class, TEdit, IKeyEntity
    where TContext : DbContext
{
    protected readonly IClock Clock;
    protected readonly IDbContextFactory<TContext> ContextFactory;

    protected BaseKeyRepository(IDbContextFactory<TContext> contextFactory, IClock clock, ILoggerFactory loggerFactory)
    {
        ContextFactory = contextFactory;
        Clock = clock;
        Log = loggerFactory.CreateLogger(GetType());
    }

    /// <summary>
    ///     Logger categorized to the concrete repository type, not to this base class.
    /// </summary>
    protected ILogger Log { get; }

    public virtual async Task<int> Count(TFilter filter)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var query = GetQueryNoTracking(context);
        query = ApplyFilter(query, filter);

        return await query.CountAsync();
    }

    public virtual async Task<IReadOnlyList<string>> GetKeys(TFilter filter)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var query = GetQueryNoTracking(context);
        query = ApplyFilter(query, filter);

        return await query.Select(x => x.Key).ToListAsync();
    }

    public virtual async Task<IReadOnlyList<TEntity>> Get(TFilter filter)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var query = GetQueryNoTracking(context);
        query = ApplyFilter(query, filter);
        query = ApplyOrder(query);

        var dals = await query.ToListAsync();

        return dals.Select(x => x.ToEntity()).ToList();
    }

    public virtual async Task<IReadOnlyList<TEntity>> Get(IReadOnlyList<string> keys)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var dals = await GetQueryNoTracking(context).Where(x => keys.Contains(x.Key)).ToListAsync();

        return dals.Select(x => x.ToEntity()).ToList();
    }

    public virtual async Task<TEntity?> Get(string key)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var dal = await GetQueryNoTracking(context).FirstOrDefaultAsync(x => x.Key == key);

        return dal?.ToEntity();
    }

    public virtual async Task<TEntity> Create(TEdit edit)
    {
        Log.LogTrace("Create: edit={Edit}", edit);

        await using var context = await ContextFactory.CreateDbContextAsync();

        var now = Clock.Now().ToUniversalTime();
        var dal = CreateDal(edit);
        dal.Created = now;
        dal.Updated = now;
        AfterDalCreatedHook(dal, edit, context);

        GetDbSet(context).Add(dal);
        await context.SaveChangesAsync();

        // Re-read so navigation properties are populated; see BaseIdRepository.Create.
        var savedKey = dal.Key;
        dal = await GetQueryNoTracking(context).FirstOrDefaultAsync(x => x.Key == savedKey);
        return dal?.ToEntity() ?? throw GetNotFoundException(savedKey);
    }

    public virtual async Task<IReadOnlyList<string>> Create(IReadOnlyList<TEdit> edits)
    {
        Log.LogTrace("Create: {Count} edits", edits.Count);

        if (edits.Count == 0)
        {
            return [];
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var now = Clock.Now().ToUniversalTime();
        var dals = new List<TDal>();
        foreach (var edit in edits)
        {
            var dal = CreateDal(edit);
            dal.Created = now;
            dal.Updated = now;
            AfterDalCreatedHook(dal, edit, context);
            dals.Add(dal);
        }

        GetDbSet(context).AddRange(dals);
        await context.SaveChangesAsync();

        return dals.Select(x => x.Key).ToList();
    }

    public virtual async Task<TEntity> Update(string key, TEdit edit)
    {
        Log.LogTrace("Update: key={Key}", key);

        await using var context = await ContextFactory.CreateDbContextAsync();

        var dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Key == key) ?? throw GetNotFoundException(key);

        dal.Apply(edit);
        AfterDalApplyHook(dal, edit, context);

        if (context.Entry(dal).State == EntityState.Modified)
        {
            dal.Updated = Clock.Now().ToUniversalTime();
        }

        await context.SaveChangesAsync();

        var savedKey = dal.Key;
        dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Key == savedKey);
        return dal?.ToEntity() ?? throw GetNotFoundException(savedKey);
    }

    public virtual async Task<int> Update(IReadOnlyDictionary<string, TEdit> edits)
    {
        Log.LogTrace("Update: {Count} edits", edits.Count);

        if (edits.Count == 0)
        {
            return 0;
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var keys = edits.Keys.ToHashSet();
        var existing = await GetQuery(context).Where(x => keys.Contains(x.Key)).ToListAsync();

        ThrowIfAnyMissing(keys, existing);

        var dalByKey = existing.ToDictionary(x => x.Key);
        var now = Clock.Now().ToUniversalTime();

        foreach (var (key, edit) in edits)
        {
            var dal = dalByKey[key];

            dal.Apply(edit);
            AfterDalApplyHook(dal, edit, context);

            if (context.Entry(dal).State == EntityState.Modified)
            {
                dal.Updated = now;
            }
        }

        return await context.SaveChangesAsync();
    }

    public virtual Task<TEntity> Update(TEntity entity)
    {
        return Update(entity.Key, entity);
    }

    public virtual async Task<int> Update(IReadOnlyList<TEntity> entities)
    {
        Log.LogTrace("Update: {Count} entities", entities.Count);

        if (entities.Count == 0)
        {
            return 0;
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var keys = entities.Select(x => x.Key).ToHashSet();
        var existing = await GetQuery(context).Where(x => keys.Contains(x.Key)).ToListAsync();

        ThrowIfAnyMissing(keys, existing);

        var dalByKey = existing.ToDictionary(x => x.Key);
        var now = Clock.Now().ToUniversalTime();

        foreach (var entity in entities)
        {
            var dal = dalByKey[entity.Key];

            dal.Apply(entity);
            AfterDalApplyHook(dal, entity, context);

            if (context.Entry(dal).State == EntityState.Modified)
            {
                dal.Updated = now;
            }
        }

        return await context.SaveChangesAsync();
    }

    public virtual async Task<int> Delete(string key)
    {
        Log.LogTrace("Delete: key={Key}", key);

        await using var context = await ContextFactory.CreateDbContextAsync();

        var existing = await GetQuery(context).FirstOrDefaultAsync(x => x.Key == key) ?? throw GetNotFoundException(key);

        GetDbSet(context).Remove(existing);
        AfterDalDeleteHook(existing, context);

        return await context.SaveChangesAsync();
    }

    public virtual async Task<int> Delete(IReadOnlyList<string> keys)
    {
        Log.LogTrace("Delete: {Count} keys", keys.Count);

        if (keys.Count == 0)
        {
            return 0;
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var existing = await GetQuery(context).Where(x => keys.Contains(x.Key)).ToListAsync();

        ThrowIfAnyMissing(keys.ToHashSet(), existing);

        GetDbSet(context).RemoveRange(existing);

        foreach (var dal in existing)
        {
            AfterDalDeleteHook(dal, context);
        }

        return await context.SaveChangesAsync();
    }

    public virtual async Task<int> Delete(TFilter filter)
    {
        Log.LogTrace("Delete by filter");

        await using var context = await ContextFactory.CreateDbContextAsync();

        var query = GetQuery(context);
        query = ApplyFilter(query, filter);

        // Deletes in the database in one statement; the delete hooks do not run.
        return await query.ExecuteDeleteAsync();
    }

    /// <summary>
    ///     Build a new row from an edit. Assign the key here, from <c>IKeyGenService.GetKey()</c>.
    /// </summary>
    protected abstract TDal CreateDal(TEdit edit);

    /// <summary>
    ///     Turn a filter into query predicates.
    /// </summary>
    protected abstract IQueryable<TDal> ApplyFilter(IQueryable<TDal> query, TFilter filter);

    /// <inheritdoc cref="BaseIdRepository{TContext,TDal,TEntity,TFilter,TEdit}.AfterDalCreatedHook" />
    protected virtual void AfterDalCreatedHook(TDal dal, TEdit edit, TContext context)
    {
    }

    /// <inheritdoc cref="BaseIdRepository{TContext,TDal,TEntity,TFilter,TEdit}.AfterDalApplyHook" />
    protected virtual void AfterDalApplyHook(TDal dal, TEdit edit, TContext context)
    {
    }

    /// <inheritdoc cref="BaseIdRepository{TContext,TDal,TEntity,TFilter,TEdit}.AfterDalDeleteHook" />
    protected virtual void AfterDalDeleteHook(TDal dal, TContext context)
    {
    }

    /// <summary>
    ///     The exception thrown when a key has no row.
    /// </summary>
    protected virtual NotFoundException GetNotFoundException(string key)
    {
        return new NotFoundKeyException(key, typeof(TEntity).Name);
    }

    /// <inheritdoc cref="BaseIdRepository{TContext,TDal,TEntity,TFilter,TEdit}.GetDbSet" />
    protected virtual DbSet<TDal> GetDbSet(TContext context)
    {
        return context.Set<TDal>();
    }

    /// <inheritdoc cref="BaseIdRepository{TContext,TDal,TEntity,TFilter,TEdit}.GetQuery" />
    protected virtual IQueryable<TDal> GetQuery(TContext context)
    {
        return GetDbSet(context);
    }

    /// <inheritdoc cref="BaseIdRepository{TContext,TDal,TEntity,TFilter,TEdit}.GetQueryNoTracking" />
    protected virtual IQueryable<TDal> GetQueryNoTracking(TContext context)
    {
        return GetQuery(context).AsNoTracking();
    }

    /// <summary>
    ///     Default ordering: newest first. Ordering by the key itself would be arbitrary, since a random key
    ///     carries no creation order the way a Snowflake id does.
    /// </summary>
    protected virtual IQueryable<TDal> ApplyOrder(IQueryable<TDal> query)
    {
        return query.OrderByDescending(x => x.Created);
    }

    private void ThrowIfAnyMissing(HashSet<string> requested, IReadOnlyList<TDal> found)
    {
        var missing = requested.Except(found.Select(x => x.Key)).ToList();
        if (missing.Count > 0)
        {
            throw GetNotFoundException(missing[0]);
        }
    }
}
