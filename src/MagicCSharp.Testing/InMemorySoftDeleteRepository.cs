using MagicCSharp.Data.Repositories;
using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Testing;

/// <summary>
///     An <see cref="InMemoryRepository{TEntity,TEdit,TFilter}" /> with soft delete, answering as the Entity Framework
///     <c>BaseIdSoftDeleteRepository</c> does: a soft delete stamps <c>Deleted</c> and <c>Updated</c> with now, an
///     already-deleted row is stamped again, and soft-deleted rows stay visible to every read unless the filter
///     excludes them.
/// </summary>
/// <typeparam name="TEntity">The entity; needs a public <c>DateTimeOffset? Deleted</c> with a setter or <c>init</c>.</typeparam>
/// <typeparam name="TEdit">The edit the entity derives from.</typeparam>
/// <typeparam name="TFilter">The filter.</typeparam>
public class InMemorySoftDeleteRepository<TEntity, TEdit, TFilter> : InMemoryRepository<TEntity, TEdit, TFilter>, ISoftDeleteRepository<TEntity, long, TFilter>
    where TEntity : class, TEdit, IMagicEntity, IDeletedEntity
{
    /// <exception cref="InvalidOperationException">
    ///     <typeparamref name="TEntity" /> lacks a writable <c>Id</c>, <c>Created</c>, <c>Updated</c> or <c>DateTimeOffset? Deleted</c>.
    /// </exception>
    public InMemorySoftDeleteRepository(TimeProvider timeProvider, long firstId = 1)
        : base(timeProvider, firstId)
    {
        if (EntityProperties<TEntity>.DeletedProperty == null)
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name} needs a public DateTimeOffset? {EntityProperties<TEntity>.DeletedName} property with a setter or init to be soft-deleted in memory.");
        }
    }

    public virtual Task<TEntity> SoftDelete(long key)
    {
        var storedRow = FindStoredRowOrThrowNotFound(key);
        var now = TimeProvider.GetUtcNow();

        var deletedRow = EntityProperties<TEntity>.Clone(storedRow);
        EntityProperties<TEntity>.DeletedProperty!.SetValue(deletedRow, now);
        EntityProperties<TEntity>.UpdatedProperty!.SetValue(deletedRow, now);
        ReplaceStoredRow(deletedRow);

        return Task.FromResult(deletedRow);
    }

    public virtual Task<TEntity> SoftDelete(TEntity entity)
    {
        return SoftDelete(EntityProperties<TEntity>.IdOf(entity));
    }

    public virtual async Task<int> SoftDelete(IReadOnlyList<long> keys)
    {
        var distinctKeys = keys.Distinct().ToList();
        ThrowNotFoundIfAnyMissing(distinctKeys);

        foreach (var key in distinctKeys)
        {
            await SoftDelete(key);
        }

        return distinctKeys.Count;
    }

    public virtual Task<int> SoftDelete(TFilter filter)
    {
        var matchingKeys = FindMatchingRows(filter).Select(EntityProperties<TEntity>.IdOf).ToList();

        return SoftDelete(matchingKeys);
    }
}
