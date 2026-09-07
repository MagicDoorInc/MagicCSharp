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
///     by a Snowflake id.
///     <para>
///         A derived repository supplies three things: <see cref="CreateDal" /> to build a new row,
///         <see cref="ApplyFilter" /> to turn a filter into a query, and — through the DAL's own <c>ToEntity</c> and
///         <c>Apply</c> — the mapping between row and entity. Everything else is here.
///     </para>
///     <para>
///         Every method opens its own short-lived context from the factory rather than sharing one. That is what
///         makes a repository safe to hold as a singleton and safe to call concurrently: no method can see another's
///         half-applied change tracker.
///     </para>
/// </summary>
/// <typeparam name="TContext">The database context type.</typeparam>
/// <typeparam name="TDal">The row type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TFilter">The query filter type.</typeparam>
/// <typeparam name="TEdit">The type carrying the writable fields.</typeparam>
public abstract class BaseIdRepository<TContext, TDal, TEntity, TFilter, TEdit> : IRepository<TEntity, long, TEdit, TFilter>
    where TDal : class, IDalTransform<TEntity, TEdit>, IDalId, IDal
    where TEntity : class, TEdit, IIdEntity
    where TContext : DbContext
{
    protected readonly IClock Clock;
    protected readonly IDbContextFactory<TContext> ContextFactory;

    protected BaseIdRepository(IDbContextFactory<TContext> contextFactory, IClock clock, ILoggerFactory loggerFactory)
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

    public virtual async Task<IReadOnlyList<long>> GetKeys(TFilter filter)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var query = GetQueryNoTracking(context);
        query = ApplyFilter(query, filter);

        return await query.Select(x => x.Id).ToListAsync();
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

    public virtual async Task<IReadOnlyList<TEntity>> Get(IReadOnlyList<long> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var dals = await GetQueryNoTracking(context).Where(x => ids.Contains(x.Id)).ToListAsync();

        return dals.Select(x => x.ToEntity()).ToList();
    }

    public virtual async Task<TEntity?> Get(long id)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        var dal = await GetQueryNoTracking(context).FirstOrDefaultAsync(x => x.Id == id);

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

        // Re-read rather than returning the tracked instance: SaveChanges does not populate navigation
        // properties, so the caller would otherwise get an entity whose related collections are empty rather
        // than unloaded — indistinguishable from genuinely having none.
        var savedId = dal.Id;
        dal = await GetQueryNoTracking(context).FirstOrDefaultAsync(x => x.Id == savedId);
        return dal?.ToEntity() ?? throw GetNotFoundException(savedId);
    }

    public virtual async Task<IReadOnlyList<long>> Create(IReadOnlyList<TEdit> edits)
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

        return dals.Select(x => x.Id).ToList();
    }

    public virtual async Task<TEntity> Update(long id, TEdit edit)
    {
        Log.LogTrace("Update: id={Id}", id);

        await using var context = await ContextFactory.CreateDbContextAsync();

        var dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Id == id) ?? throw GetNotFoundException(id);

        dal.Apply(edit);
        AfterDalApplyHook(dal, edit, context);

        // Only stamp Updated when something actually changed, so a no-op write does not look like an edit
        // in the audit trail or invalidate a cache keyed on the timestamp.
        if (context.Entry(dal).State == EntityState.Modified)
        {
            dal.Updated = Clock.Now().ToUniversalTime();
        }

        await context.SaveChangesAsync();

        // Re-read: changing a foreign key clears the navigation property that pointed through it.
        var savedId = dal.Id;
        dal = await GetQuery(context).FirstOrDefaultAsync(x => x.Id == savedId);
        return dal?.ToEntity() ?? throw GetNotFoundException(savedId);
    }

    public virtual async Task<int> Update(IReadOnlyDictionary<long, TEdit> edits)
    {
        Log.LogTrace("Update: {Count} edits", edits.Count);

        if (edits.Count == 0)
        {
            return 0;
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var ids = edits.Keys.ToHashSet();
        var existing = await GetQuery(context).Where(x => ids.Contains(x.Id)).ToListAsync();

        ThrowIfAnyMissing(ids, existing);

        var dalById = existing.ToDictionary(x => x.Id);
        var now = Clock.Now().ToUniversalTime();

        foreach (var (id, edit) in edits)
        {
            var dal = dalById[id];

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
        return Update(entity.Id, entity);
    }

    public virtual async Task<int> Update(IReadOnlyList<TEntity> entities)
    {
        Log.LogTrace("Update: {Count} entities", entities.Count);

        if (entities.Count == 0)
        {
            return 0;
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var ids = entities.Select(x => x.Id).ToHashSet();
        var existing = await GetQuery(context).Where(x => ids.Contains(x.Id)).ToListAsync();

        ThrowIfAnyMissing(ids, existing);

        var dalById = existing.ToDictionary(x => x.Id);
        var now = Clock.Now().ToUniversalTime();

        foreach (var entity in entities)
        {
            var dal = dalById[entity.Id];

            dal.Apply(entity);
            AfterDalApplyHook(dal, entity, context);

            if (context.Entry(dal).State == EntityState.Modified)
            {
                dal.Updated = now;
            }
        }

        return await context.SaveChangesAsync();
    }

    public virtual async Task<int> Delete(long id)
    {
        Log.LogTrace("Delete: id={Id}", id);

        await using var context = await ContextFactory.CreateDbContextAsync();

        var existing = await GetQuery(context).FirstOrDefaultAsync(x => x.Id == id) ?? throw GetNotFoundException(id);

        GetDbSet(context).Remove(existing);
        AfterDalDeleteHook(existing, context);

        return await context.SaveChangesAsync();
    }

    public virtual async Task<int> Delete(IReadOnlyList<long> ids)
    {
        Log.LogTrace("Delete: {Count} ids", ids.Count);

        if (ids.Count == 0)
        {
            return 0;
        }

        await using var context = await ContextFactory.CreateDbContextAsync();

        var existing = await GetQuery(context).Where(x => ids.Contains(x.Id)).ToListAsync();

        ThrowIfAnyMissing(ids.ToHashSet(), existing);

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

        // Deletes in the database in one statement. The delete hooks do not run, because no row is
        // materialized — override this method if a filter delete needs them.
        return await query.ExecuteDeleteAsync();
    }

    /// <summary>
    ///     Build a new row from an edit. Assign the id here, from <c>IKeyGenService.GetId()</c>.
    /// </summary>
    protected abstract TDal CreateDal(TEdit edit);

    /// <summary>
    ///     Turn a filter into query predicates. Called for every read, and for
    ///     <see cref="Delete(TFilter)" />.
    /// </summary>
    protected abstract IQueryable<TDal> ApplyFilter(IQueryable<TDal> query, TFilter filter);

    /// <summary>
    ///     Runs after a row is built but before it is added. Use for values the DAL cannot derive from the edit
    ///     alone, such as a related row that has to be looked up on the same context.
    /// </summary>
    protected virtual void AfterDalCreatedHook(TDal dal, TEdit edit, TContext context)
    {
    }

    /// <summary>
    ///     Runs after an edit is applied to a row but before it is saved.
    /// </summary>
    protected virtual void AfterDalApplyHook(TDal dal, TEdit edit, TContext context)
    {
    }

    /// <summary>
    ///     Runs after a row is marked for removal but before it is saved. Use to clean up rows that would
    ///     otherwise be orphaned.
    /// </summary>
    protected virtual void AfterDalDeleteHook(TDal dal, TContext context)
    {
    }

    /// <summary>
    ///     The exception thrown when an id has no row. Override to return a more specific type so callers can
    ///     catch this entity's absence in particular.
    /// </summary>
    protected virtual NotFoundException GetNotFoundException(long id)
    {
        return new NotFoundIdException(id, typeof(TEntity).Name);
    }

    /// <summary>
    ///     The set this repository reads and writes. Override only when the DAL is not the context's default set
    ///     for its type.
    /// </summary>
    protected virtual DbSet<TDal> GetDbSet(TContext context)
    {
        return context.Set<TDal>();
    }

    /// <summary>
    ///     The tracked query used by writes. Override to <c>Include</c> the navigation properties this entity's
    ///     <c>ToEntity</c> reads, so they are loaded rather than silently empty.
    /// </summary>
    protected virtual IQueryable<TDal> GetQuery(TContext context)
    {
        return GetDbSet(context);
    }

    /// <summary>
    ///     The untracked query used by reads. Skipping change tracking is what keeps a read-only query from
    ///     paying to snapshot every row it returns.
    /// </summary>
    protected virtual IQueryable<TDal> GetQueryNoTracking(TContext context)
    {
        return GetQuery(context).AsNoTracking();
    }

    /// <summary>
    ///     Default ordering for unpaginated and paginated reads: newest first, since a Snowflake id sorts by
    ///     creation time.
    /// </summary>
    protected virtual IQueryable<TDal> ApplyOrder(IQueryable<TDal> query)
    {
        return query.OrderByDescending(x => x.Id);
    }

    /// <summary>
    ///     Fail the whole batch when any requested id is absent, rather than silently writing the subset that
    ///     happened to exist.
    /// </summary>
    private void ThrowIfAnyMissing(HashSet<long> requested, IReadOnlyList<TDal> found)
    {
        var missing = requested.Except(found.Select(x => x.Id)).ToList();
        if (missing.Count > 0)
        {
            throw GetNotFoundException(missing[0]);
        }
    }
}
