namespace MagicCSharp.Infrastructure.Entities;

/// <summary>
///     An entity that is deleted by marking it rather than removing the row.
/// </summary>
public interface IDeletedEntity
{
    /// <summary>
    ///     When the entity was deleted, or null while it is live. A value here means the entity should be treated
    ///     as gone by everything except history, audit and recovery.
    /// </summary>
    public DateTimeOffset? Deleted { get; }
}
