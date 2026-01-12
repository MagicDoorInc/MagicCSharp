namespace MagicCSharp.Infrastructure.Entities;

/// <summary>
///     Interface for entities that use a string key as their primary identifier.
/// </summary>
public interface IKeyEntity : IMagicEntity
{
    /// <summary>
    ///     The string key that uniquely identifies this entity.
    /// </summary>
    string Key { get; }
}