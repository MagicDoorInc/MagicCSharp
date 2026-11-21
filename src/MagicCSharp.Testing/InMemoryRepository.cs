using System.Reflection;
using MagicCSharp.Data.Models;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure.Exceptions;

namespace MagicCSharp.Testing;

/// <summary>
///     A repository that keeps its rows in memory and answers every call the way the Entity Framework
///     <c>BaseIdRepository</c> and <c>BaseIdPaginatedRepository</c> do, for functional tests of use cases.
///     <para>
///         A fake inherits it, lists its repository interface, and writes only what the interface adds. It needs no
///         mapping: a new row copies every property of the edit by name (the entity derives from the edit), an update
///         copies them onto the stored row, and a filter that lists <see cref="IEntityFilter{TEntity}" /> selects rows
///         with its generated <c>Matches</c>, the same rows the database query selects.
///     </para>
///     <para>
///         Every interface member is virtual, so a fake keeps special behaviour (a uniqueness rule, a failure on an
///         operation its tests never use) as an override. Bulk members check every key first, then call the single
///         member once per item, so overriding the single member changes the bulk path too.
///     </para>
///     <para>
///         Ids count up from <c>firstId</c>; times come from the given <see cref="System.TimeProvider" />. Reads return
///         the stored instance, so entities are expected to be immutable records, as the repository bases require.
///         Not thread-safe.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The entity; needs a public <c>long Id</c> with a setter or <c>init</c>.</typeparam>
/// <typeparam name="TEdit">The edit the entity derives from.</typeparam>
/// <typeparam name="TFilter">The filter; lists <see cref="IEntityFilter{TEntity}" />, or the fake overrides <see cref="Matches" />.</typeparam>
public class InMemoryRepository<TEntity, TEdit, TFilter> : IRepository<TEntity, long, TEdit, TFilter>, IPaginatedRepository<TEntity, TFilter>,
    ITenantScopedDestroyer
    where TEntity : class, TEdit, IMagicEntity
{
    private readonly Dictionary<long, TEntity> storedRows = [];

    private long nextId;

    /// <exception cref="InvalidOperationException">
    ///     <typeparamref name="TEntity" /> has no public <c>long Id</c>, <c>DateTimeOffset Created</c> or
    ///     <c>DateTimeOffset Updated</c> with a setter or <c>init</c>.
    /// </exception>
    public InMemoryRepository(TimeProvider timeProvider, long firstId = 1)
    {
        ThrowIfNotWritable(EntityProperties<TEntity>.IdProperty, EntityProperties<TEntity>.IdName, typeof(long));
        ThrowIfNotWritable(EntityProperties<TEntity>.CreatedProperty, EntityProperties<TEntity>.CreatedName, typeof(DateTimeOffset));
        ThrowIfNotWritable(EntityProperties<TEntity>.UpdatedProperty, EntityProperties<TEntity>.UpdatedName, typeof(DateTimeOffset));

        TimeProvider = timeProvider;
        nextId = firstId;
    }

    /// <summary>Every stored row, soft-deleted ones too, lowest id first.</summary>
    public IReadOnlyList<TEntity> StoredRowsByIdAscending => storedRows.Values.OrderBy(EntityProperties<TEntity>.IdOf).ToList();

    protected TimeProvider TimeProvider { get; }

    /// <summary>
    ///     The entity's tenant column, a public <c>long</c> or <c>long?</c>. <see cref="DestroyTenant" /> removes the
    ///     rows whose value equals the tenant, and an update never changes it. Null, or a name the entity doesn't have
    ///     as a <c>long</c>, means the rows belong to no tenant: <see cref="DestroyTenant" /> removes nothing.
    /// </summary>
    protected virtual string? TenantKeyPropertyName => null;

    public virtual Task<int> Count(TFilter filter)
    {
        return Task.FromResult(FindMatchingRows(filter).Count());
    }

    public virtual Task<IReadOnlyList<long>> GetKeys(TFilter filter)
    {
        IReadOnlyList<long> keys = FindMatchingRows(filter).Select(EntityProperties<TEntity>.IdOf).ToList();

        return Task.FromResult(keys);
    }

    public virtual Task<IReadOnlyList<TEntity>> Get(TFilter filter)
    {
        IReadOnlyList<TEntity> entities = ApplyOrder(FindMatchingRows(filter)).ToList();

        return Task.FromResult(entities);
    }

    public virtual Task<IReadOnlyList<TEntity>> Get(IReadOnlyList<long> keys)
    {
        IReadOnlyList<TEntity> entities = keys.Distinct().Where(storedRows.ContainsKey).Select(key => storedRows[key]).ToList();

        return Task.FromResult(entities);
    }

    public virtual Task<TEntity?> Get(long key)
    {
        return Task.FromResult(storedRows.GetValueOrDefault(key));
    }

    public virtual Task<Pagination<TEntity>> Get(PaginationRequest pagination, TFilter filter)
    {
        var orderedRows = ApplyOrder(FindMatchingRows(filter)).ToList();
        IReadOnlyList<TEntity> pageRows = orderedRows;
        if (!pagination.IsDisabled && pagination.PageSize != 0)
        {
            pageRows = orderedRows.Skip(pagination.Skip).Take(pagination.PageSize).ToList();
        }

        return Task.FromResult(new Pagination<TEntity>(pagination, orderedRows.Count, pageRows));
    }

    public virtual Task<TEntity> Create(TEdit edit)
    {
        return Task.FromResult(StoreNewRow(edit));
    }

    public virtual async Task<IReadOnlyList<long>> Create(IReadOnlyList<TEdit> edits)
    {
        var keys = new List<long>();
        foreach (var edit in edits)
        {
            var entity = await Create(edit);
            keys.Add(EntityProperties<TEntity>.IdOf(entity));
        }

        return keys;
    }

    /// <summary>
    ///     Stores a row as the database already holds it: the next id, <c>Created</c> and <c>Updated</c> now. Never
    ///     calls <see cref="Create(TEdit)" />, so a fake whose <c>Create</c> is made to fail can still be seeded.
    /// </summary>
    public TEntity SeedStoredRow(TEdit edit)
    {
        return StoreNewRow(edit);
    }

    /// <summary>
    ///     Copies the edit onto the stored row; <c>Updated</c> moves only when the result differs from the stored row,
    ///     as the database repositories stamp only modified rows.
    /// </summary>
    public virtual Task<TEntity> Update(long key, TEdit edit)
    {
        var storedRow = FindStoredRowOrThrowNotFound(key);
        var editedRow = CopyWithEdit(storedRow, edit);
        if (editedRow.Equals(storedRow))
        {
            return Task.FromResult(storedRow);
        }

        var updatedRow = EntityProperties<TEntity>.Clone(editedRow);
        EntityProperties<TEntity>.UpdatedProperty!.SetValue(updatedRow, TimeProvider.GetUtcNow());
        ReplaceStoredRow(updatedRow);

        return Task.FromResult(updatedRow);
    }

    /// <returns>The number of rows whose values changed.</returns>
    public virtual async Task<int> Update(IReadOnlyDictionary<long, TEdit> edits)
    {
        ThrowNotFoundIfAnyMissing(edits.Keys);

        var changedRowCount = 0;
        foreach (var (key, edit) in edits)
        {
            var storedRow = storedRows[key];
            await Update(key, edit);
            changedRowCount += CountIfReplaced(key, storedRow);
        }

        return changedRowCount;
    }

    public virtual Task<TEntity> Update(TEntity entity)
    {
        return Update(EntityProperties<TEntity>.IdOf(entity), entity);
    }

    /// <returns>The number of rows whose values changed.</returns>
    public virtual async Task<int> Update(IReadOnlyList<TEntity> entities)
    {
        ThrowNotFoundIfAnyMissing(entities.Select(EntityProperties<TEntity>.IdOf));

        var changedRowCount = 0;
        foreach (var entity in entities)
        {
            var key = EntityProperties<TEntity>.IdOf(entity);
            var storedRow = storedRows[key];
            await Update(entity);
            changedRowCount += CountIfReplaced(key, storedRow);
        }

        return changedRowCount;
    }

    public virtual Task<int> Delete(long key)
    {
        FindStoredRowOrThrowNotFound(key);
        storedRows.Remove(key);

        return Task.FromResult(1);
    }

    public virtual async Task<int> Delete(IReadOnlyList<long> keys)
    {
        var distinctKeys = keys.Distinct().ToList();
        ThrowNotFoundIfAnyMissing(distinctKeys);

        var deletedRowCount = 0;
        foreach (var key in distinctKeys)
        {
            deletedRowCount += await Delete(key);
        }

        return deletedRowCount;
    }

    public virtual Task<int> Delete(TFilter filter)
    {
        var matchingKeys = FindMatchingRows(filter).Select(EntityProperties<TEntity>.IdOf).ToList();

        return Delete(matchingKeys);
    }

    /// <summary>Hard-removes every row of the tenant, soft-deleted ones too, as the database repositories do.</summary>
    /// <returns>The number of rows removed; 0 when <see cref="TenantKeyPropertyName" /> names no tenant column.</returns>
    public virtual Task<int> DestroyTenant(long tenantId)
    {
        var tenantKeyReader = TenantKeyPropertyName == null ? null : EntityProperties<TEntity>.FindLongReader(TenantKeyPropertyName);
        if (tenantKeyReader == null)
        {
            return Task.FromResult(0);
        }

        var tenantKeys = storedRows.Values.Where(entity => tenantKeyReader(entity) == tenantId).Select(EntityProperties<TEntity>.IdOf).ToList();
        foreach (var key in tenantKeys)
        {
            storedRows.Remove(key);
        }

        return Task.FromResult(tenantKeys.Count);
    }

    /// <summary>
    ///     Builds a new row: every property of the edit with a public setter or <c>init</c> copied by name, then
    ///     <c>Id</c>, <c>Created</c> and <c>Updated</c>. Entity properties the edit doesn't hold stay at their default;
    ///     a fake that needs them set overrides this.
    /// </summary>
    /// <exception cref="InvalidOperationException">The entity has no public parameterless constructor.</exception>
    protected virtual TEntity CreateEntity(TEdit edit, long id, DateTimeOffset created)
    {
        var entity = CreateEmptyEntity();
        CopyEditProperties(edit, entity, null);
        EntityProperties<TEntity>.IdProperty!.SetValue(entity, id);
        EntityProperties<TEntity>.CreatedProperty!.SetValue(entity, created);
        EntityProperties<TEntity>.UpdatedProperty!.SetValue(entity, created);

        return entity;
    }

    /// <summary>
    ///     A copy of the stored row with every property of the edit copied onto it, except the tenant key, which never
    ///     changes. Properties the edit doesn't hold keep their stored value. Never changes <paramref name="entity" />.
    /// </summary>
    protected virtual TEntity CopyWithEdit(TEntity entity, TEdit edit)
    {
        var editedEntity = EntityProperties<TEntity>.Clone(entity);
        CopyEditProperties(edit, editedEntity, TenantKeyPropertyName);

        return editedEntity;
    }

    /// <exception cref="InvalidOperationException">The filter doesn't list <see cref="IEntityFilter{TEntity}" />.</exception>
    protected virtual bool Matches(TEntity entity, TFilter filter)
    {
        var entityFilter = filter as IEntityFilter<TEntity>;
        if (entityFilter == null)
        {
            throw new InvalidOperationException(
                $"{typeof(TFilter).Name} does not implement IEntityFilter<{typeof(TEntity).Name}>. Make it a partial class listing that interface, " +
                $"so its Matches is generated, or override Matches in {GetType().Name}.");
        }

        return entityFilter.Matches(entity);
    }

    /// <summary>The order of filtered reads and pages: newest id first, as the database repositories' default <c>ApplyOrder</c>.</summary>
    protected virtual IEnumerable<TEntity> ApplyOrder(IEnumerable<TEntity> entities)
    {
        return entities.OrderByDescending(EntityProperties<TEntity>.IdOf);
    }

    /// <summary>Builds, never throws, the exception a missing id raises; override it to raise the consumer's own type.</summary>
    protected virtual Exception CreateNotFoundException(long id)
    {
        return new NotFoundIdException(id, typeof(TEntity).Name);
    }

    /// <exception cref="Exception">The result of <see cref="CreateNotFoundException" /> when no row has the id.</exception>
    protected TEntity FindStoredRowOrThrowNotFound(long id)
    {
        if (!storedRows.TryGetValue(id, out var storedRow))
        {
            throw CreateNotFoundException(id);
        }

        return storedRow;
    }

    /// <summary>Stores the entity under its id, replacing the row there.</summary>
    protected void ReplaceStoredRow(TEntity entity)
    {
        storedRows[EntityProperties<TEntity>.IdOf(entity)] = entity;
    }

    /// <exception cref="Exception">The result of <see cref="CreateNotFoundException" /> for the first missing id.</exception>
    protected void ThrowNotFoundIfAnyMissing(IEnumerable<long> ids)
    {
        foreach (var id in ids)
        {
            FindStoredRowOrThrowNotFound(id);
        }
    }

    /// <summary>The stored rows the filter selects, through <see cref="Matches" />, in no particular order.</summary>
    protected IEnumerable<TEntity> FindMatchingRows(TFilter filter)
    {
        return storedRows.Values.Where(entity => Matches(entity, filter));
    }

    private static void ThrowIfNotWritable(PropertyInfo? property, string propertyName, Type propertyType)
    {
        if (property == null)
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name} needs a public {propertyType.Name} {propertyName} property with a setter or init to be kept in memory.");
        }
    }

    private static TEntity CreateEmptyEntity()
    {
        if (typeof(TEntity).GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name} has no public parameterless constructor; override CreateEntity to build it.");
        }

        return Activator.CreateInstance<TEntity>();
    }

    private static void CopyEditProperties(TEdit edit, TEntity entity, string? skippedPropertyName)
    {
        foreach (var editProperty in EditProperties.Writable)
        {
            if (editProperty.Name == skippedPropertyName)
            {
                continue;
            }

            editProperty.SetValue(entity, editProperty.GetValue(edit));
        }
    }

    private TEntity StoreNewRow(TEdit edit)
    {
        var entity = CreateEntity(edit, nextId, TimeProvider.GetUtcNow());
        nextId++;
        ReplaceStoredRow(entity);

        return entity;
    }

    private int CountIfReplaced(long key, TEntity previousRow)
    {
        if (storedRows.TryGetValue(key, out var currentRow) && ReferenceEquals(currentRow, previousRow))
        {
            return 0;
        }

        return 1;
    }

    /// <summary>The edit's properties a new row and an update copy: public, readable, and writable through a public setter or <c>init</c>.</summary>
    private static class EditProperties
    {
        public static readonly IReadOnlyList<PropertyInfo> Writable = typeof(TEdit).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod?.IsPublic == true && property.SetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0)
            .ToList();
    }
}
