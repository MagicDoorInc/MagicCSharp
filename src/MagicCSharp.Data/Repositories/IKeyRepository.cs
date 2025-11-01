using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Data.Repositories;

/// <summary>
/// Repository interface for entities that use string keys as primary identifiers.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TFilter">The filter type for querying.</typeparam>
/// <typeparam name="TEdit">The edit type for create/update operations.</typeparam>
public interface IKeyRepository<TEntity, in TFilter, in TEdit>
    where TEntity : class, TEdit, IKeyEntity
{
    /// <summary>
    /// Get the count of entities matching the filter.
    /// </summary>
    Task<int> Count(TFilter filter);

    /// <summary>
    /// Get all entities matching the filter.
    /// </summary>
    Task<IReadOnlyList<TEntity>> Get(TFilter filter);

    /// <summary>
    /// Get a single entity by its key.
    /// </summary>
    /// <param name="key">The string key to search for.</param>
    /// <returns>The entity if found, null otherwise.</returns>
    Task<TEntity?> Get(string key);

    /// <summary>
    /// Get multiple entities by their keys.
    /// </summary>
    /// <param name="keys">The list of string keys to search for.</param>
    /// <returns>The list of found entities.</returns>
    Task<IReadOnlyList<TEntity>> Get(IReadOnlyList<string> keys);

    /// <summary>
    /// Create a new entity.
    /// </summary>
    /// <param name="edit">The entity data to create.</param>
    /// <returns>The created entity.</returns>
    Task<TEntity> Create(TEdit edit);

    /// <summary>
    /// Create multiple entities.
    /// </summary>
    /// <param name="edits">The list of entity data to create.</param>
    /// <returns>The list of keys for the created entities.</returns>
    Task<IReadOnlyList<string>> Create(IReadOnlyList<TEdit> edits);

    /// <summary>
    /// Update an existing entity.
    /// </summary>
    /// <param name="key">The key of the entity to update.</param>
    /// <param name="edit">The new entity data.</param>
    /// <returns>The updated entity.</returns>
    Task<TEntity> Update(string key, TEdit edit);

    /// <summary>
    /// Update an existing entity using the entity's key.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>The updated entity.</returns>
    Task<TEntity> Update(TEntity entity);

    /// <summary>
    /// Delete an entity by its key.
    /// </summary>
    /// <param name="key">The key of the entity to delete.</param>
    Task Delete(string key);
}
