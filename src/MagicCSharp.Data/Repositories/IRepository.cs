namespace MagicCSharp.Data.Repositories;

/// <summary>
///     Base repository interface for entities with numeric IDs.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TFilter">The filter type for queries.</typeparam>
/// <typeparam name="TEdit">The edit type for create/update operations.</typeparam>
public interface IRepository<TEntity, in TFilter, in TEdit>
{
    /// <summary>
    ///     Get the count of entities matching the filter.
    /// </summary>
    Task<int> Count(TFilter filter);

    /// <summary>
    ///     Get all entities matching the filter.
    /// </summary>
    Task<IReadOnlyList<TEntity>> Get(TFilter filter);

    /// <summary>
    ///     Get multiple entities by their IDs.
    /// </summary>
    Task<IReadOnlyList<TEntity>> Get(IReadOnlyList<long> ids);

    /// <summary>
    ///     Get a single entity by ID.
    /// </summary>
    Task<TEntity?> Get(long id);

    /// <summary>
    ///     Create a new entity.
    /// </summary>
    Task<TEntity> Create(TEdit edit);

    /// <summary>
    ///     Create multiple entities.
    /// </summary>
    /// <returns>List of IDs of created entities.</returns>
    Task<IReadOnlyList<long>> Create(IReadOnlyList<TEdit> edits);

    /// <summary>
    ///     Update an existing entity.
    /// </summary>
    Task<TEntity> Update(long id, TEdit edit);

    /// <summary>
    ///     Update an existing entity using the entity itself.
    /// </summary>
    Task<TEntity> Update(TEntity entity);

    /// <summary>
    ///     Delete an entity by ID.
    /// </summary>
    Task Delete(long id);

    /// <summary>
    ///     Delete multiple entities by IDs.
    /// </summary>
    Task Delete(IReadOnlyList<long> ids);
}