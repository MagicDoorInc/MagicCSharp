namespace MagicCSharp.Data.Repositories;

/// <summary>
///     The full read/write surface of a repository, over whatever type the entity is keyed by.
///     <para>
///         <typeparamref name="TKey" /> is a parameter rather than a fixed <c>long</c> so the same contract covers
///         an entity keyed by a Snowflake id and one keyed by a public string key, without a parallel interface
///         for each. Constrain it to what your entity actually uses: <c>IRepository&lt;Order, long, OrderEdit,
///         OrderFilter&gt;</c> or <c>IRepository&lt;ApiKey, string, ApiKeyEdit, ApiKeyFilter&gt;</c>.
///     </para>
///     <para>
///         Pagination, soft delete and search keywords are deliberately not here — a repository opts into each by
///         also implementing <see cref="IPaginatedRepository{TEntity,TFilter}" />,
///         <see cref="ISoftDeleteRepository{TEntity,TKey,TFilter}" /> or <see cref="ISearchRepository{TKey}" />, so
///         a caller can see from the interface which of them the entity supports.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The entity type returned by reads.</typeparam>
/// <typeparam name="TKey">The entity's primary key type, typically <c>long</c> or <c>string</c>.</typeparam>
/// <typeparam name="TEdit">The type carrying the writable fields. <typeparamref name="TEntity" /> derives from it.</typeparam>
/// <typeparam name="TFilter">The query filter type.</typeparam>
public interface IRepository<TEntity, TKey, TEdit, TFilter>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    ///     Count the entities matching the filter, without loading them.
    /// </summary>
    Task<int> Count(TFilter filter);

    /// <summary>
    ///     Get just the keys of the entities matching the filter. Cheaper than <see cref="Get(TFilter)" /> when the
    ///     caller only needs to know which entities matched.
    /// </summary>
    Task<IReadOnlyList<TKey>> GetKeys(TFilter filter);

    /// <summary>
    ///     Get the entities matching the filter.
    /// </summary>
    Task<IReadOnlyList<TEntity>> Get(TFilter filter);

    /// <summary>
    ///     Get the entities with these keys. Missing keys are skipped rather than throwing, so the result may be
    ///     shorter than <paramref name="keys" />.
    /// </summary>
    Task<IReadOnlyList<TEntity>> Get(IReadOnlyList<TKey> keys);

    /// <summary>
    ///     Get one entity by key, or null when it does not exist.
    /// </summary>
    Task<TEntity?> Get(TKey key);

    /// <summary>
    ///     Create one entity.
    /// </summary>
    /// <returns>The created entity, re-read so navigation properties are populated.</returns>
    Task<TEntity> Create(TEdit edit);

    /// <summary>
    ///     Create several entities in one round trip.
    /// </summary>
    /// <returns>The keys of the created entities, in the order the edits were given.</returns>
    Task<IReadOnlyList<TKey>> Create(IReadOnlyList<TEdit> edits);

    /// <summary>
    ///     Apply an edit to the entity with this key.
    /// </summary>
    /// <returns>The updated entity, re-read so navigation properties are populated.</returns>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">No entity has this key.</exception>
    Task<TEntity> Update(TKey key, TEdit edit);

    /// <summary>
    ///     Apply an edit to each of several entities in one round trip. Nothing is written if any key is missing.
    /// </summary>
    /// <returns>The number of rows actually changed.</returns>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">A key has no entity.</exception>
    Task<int> Update(IReadOnlyDictionary<TKey, TEdit> edits);

    /// <summary>
    ///     Write back an entity that was read, edited in memory, and is being saved whole.
    /// </summary>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">The entity no longer exists.</exception>
    Task<TEntity> Update(TEntity entity);

    /// <summary>
    ///     Write back several entities in one round trip. Nothing is written if any of them no longer exists.
    /// </summary>
    /// <returns>The number of rows actually changed.</returns>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">An entity no longer exists.</exception>
    Task<int> Update(IReadOnlyList<TEntity> entities);

    /// <summary>
    ///     Permanently delete the entity with this key. For entities that should be recoverable, implement
    ///     <see cref="ISoftDeleteRepository{TEntity,TKey,TFilter}" /> and use that instead.
    /// </summary>
    /// <returns>The number of rows deleted.</returns>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">No entity has this key.</exception>
    Task<int> Delete(TKey key);

    /// <summary>
    ///     Permanently delete several entities. Nothing is deleted if any key is missing.
    /// </summary>
    /// <returns>The number of rows deleted.</returns>
    /// <exception cref="Infrastructure.Exceptions.NotFoundException">A key has no entity.</exception>
    Task<int> Delete(IReadOnlyList<TKey> keys);

    /// <summary>
    ///     Permanently delete every entity matching the filter, in the database rather than by loading them first.
    /// </summary>
    /// <returns>The number of rows deleted.</returns>
    Task<int> Delete(TFilter filter);
}
