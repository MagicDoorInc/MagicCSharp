using System.Diagnostics.CodeAnalysis;
using MagicCSharp.Infrastructure.Entities;

namespace MagicCSharp.Infrastructure.Exceptions;

/// <summary>
///     Exception thrown when a requested entity or resource is not found.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the NotFoundException class.
    /// </summary>
    public NotFoundException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the NotFoundException class with a specified entity type.
    /// </summary>
    /// <param name="type">The type of entity that was not found.</param>
    public NotFoundException(string type) : base($"{type} not found.")
    {
    }

    /// <summary>
    ///     Gets debug data for logging purposes. Override in derived classes to provide additional context.
    /// </summary>
    /// <returns>A JSON string with debug information, or null if none available.</returns>
    public virtual string? GetDebugData()
    {
        return null;
    }

    /// <summary>
    ///     Throws a NotFoundException if the value is null.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check for null.</param>
    /// <param name="key">Optional key identifier for the entity.</param>
    /// <exception cref="NotFoundKeyException">Thrown when value is null and key is provided.</exception>
    /// <exception cref="NotFoundException">Thrown when value is null and no key is provided.</exception>
    public static void ThrowIfNull<T>([NotNull] T? value, string? key = null)
    {
        if (value == null)
        {
            if (key != null)
            {
                throw new NotFoundKeyException(key, typeof(T).Name);
            }

            throw new NotFoundException(typeof(T).Name);
        }
    }

    /// <summary>
    ///     Throws a NotFoundException if the value is null.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check for null.</param>
    /// <param name="id">Optional numeric identifier for the entity.</param>
    /// <exception cref="NotFoundIdException">Thrown when value is null and id is provided.</exception>
    /// <exception cref="NotFoundException">Thrown when value is null and no id is provided.</exception>
    public static void ThrowIfNull<T>([NotNull] T? value, long? id = null)
    {
        if (value == null)
        {
            if (id != null)
            {
                throw new NotFoundIdException(id.Value, typeof(T).Name);
            }

            throw new NotFoundException(typeof(T).Name);
        }
    }

    /// <summary>
    ///     Validates that all expected entity IDs are present in the provided collection.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that implements IIdEntity.</typeparam>
    /// <param name="entities">The collection of entities to check.</param>
    /// <param name="expectedIds">The list of IDs that should be present.</param>
    /// <exception cref="NotFoundIdException">Thrown when one or more expected IDs are missing.</exception>
    public static void ThrowIfMissing<TEntity>(IEnumerable<TEntity> entities, IReadOnlyList<long> expectedIds)
        where TEntity : IIdEntity
    {
        var entityIds = entities.Select(x => x.Id).ToList();
        var missingIds = expectedIds.Except(entityIds).ToList();
        if (missingIds.Count != 0)
        {
            throw new NotFoundIdException(missingIds.First(), typeof(TEntity).Name);
        }
    }
}

/// <summary>
///     Exception thrown when an entity with a specific numeric ID is not found.
/// </summary>
/// <param name="id">The ID that was not found.</param>
/// <param name="type">The type of entity that was not found.</param>
public class NotFoundIdException(
    long id,
    string type) : NotFoundException(type)
{
    /// <summary>
    ///     Gets the ID of the entity that was not found.
    /// </summary>
    public long Id { get; } = id;

    /// <summary>
    ///     Gets debug data containing the ID that was not found.
    /// </summary>
    /// <returns>A JSON string with the ID.</returns>
    public override string? GetDebugData()
    {
        return "{\"id\": " + Id + "}";
    }
}

/// <summary>
///     Exception thrown when an entity with a specific string key is not found.
/// </summary>
/// <param name="key">The key that was not found.</param>
/// <param name="type">The type of entity that was not found.</param>
public class NotFoundKeyException(
    string key,
    string type) : NotFoundException(type)
{
    /// <summary>
    ///     Gets the key of the entity that was not found.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    ///     Gets debug data containing the key that was not found.
    /// </summary>
    /// <returns>A JSON string with the key.</returns>
    public override string? GetDebugData()
    {
        return "{\"key\": \"" + Key + "\"}";
    }
}