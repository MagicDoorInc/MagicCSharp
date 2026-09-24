using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Data.Repositories;

/// <summary>
///     Opt-in recoverable deletion: the row stays, with a timestamp marking when it was removed from view.
///     <para>
///         Use this instead of <see cref="IRepository{TEntity,TKey,TEdit,TFilter}.Delete(TKey)" /> for anything a
///         user can delete by mistake, anything referenced by history or an audit trail, and anything a foreign key
///         still points at. The two coexist on purpose — a repository can offer soft delete for normal use and hard
///         delete for genuine purges.
///     </para>
///     <para>
///         Soft-deleted rows still come back from queries unless the filter excludes them. Exclude them in the
///         repository's own filter application so callers cannot forget.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The entity type, which must carry the deletion timestamp.</typeparam>
/// <typeparam name="TKey">The entity's primary key type.</typeparam>
/// <typeparam name="TFilter">The query filter type.</typeparam>
public interface ISoftDeleteRepository<TEntity, TKey, TFilter>
    where TEntity : IDeletedEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    ///     Mark the entity with this key deleted.
    /// </summary>
    /// <returns>The entity as stored after the change, so a caller can assert on it.</returns>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">No entity has this key.</exception>
    Task<TEntity> SoftDelete(TKey key);

    /// <summary>
    ///     Mark this entity deleted.
    /// </summary>
    /// <returns>The entity as stored after the change.</returns>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">The entity no longer exists.</exception>
    Task<TEntity> SoftDelete(TEntity entity);

    /// <summary>
    ///     Mark several entities deleted in one round trip. Nothing is written if any key is missing.
    /// </summary>
    /// <returns>The number of rows changed.</returns>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">A key has no entity.</exception>
    Task<int> SoftDelete(IReadOnlyList<TKey> keys);

    /// <summary>
    ///     Mark every entity matching the filter deleted.
    /// </summary>
    /// <returns>The number of rows changed.</returns>
    Task<int> SoftDelete(TFilter filter);
}
