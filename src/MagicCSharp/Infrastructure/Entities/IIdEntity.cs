namespace MagicCSharp.Infrastructure.Entities;

/// <summary>
///     Represents an entity with a numeric identifier.
/// </summary>
public interface IIdEntity
{
    /// <summary>
    ///     Gets the unique identifier for this entity.
    /// </summary>
    public long Id { get; }
}