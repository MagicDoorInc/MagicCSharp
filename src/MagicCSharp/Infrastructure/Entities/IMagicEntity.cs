namespace MagicCSharp.Infrastructure.Entities;

/// <summary>
/// Base interface for all entities with timestamp tracking.
/// </summary>
public interface IMagicEntity
{
    /// <summary>
    /// When the entity was created.
    /// </summary>
    public DateTimeOffset Created { get; init; }

    /// <summary>
    /// When the entity was last updated.
    /// </summary>
    public DateTimeOffset Updated { get; init; }
}
