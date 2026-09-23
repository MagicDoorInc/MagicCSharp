namespace MagicCSharp.Data.EntityFramework.Dals;

/// <summary>
///     A DAL whose rows are deleted by marking them rather than removing them. Required by the soft-delete
///     repository bases.
/// </summary>
public interface IDalDeleted
{
    /// <summary>
    ///     When the row was soft-deleted, or null while it is live.
    /// </summary>
    public DateTimeOffset? Deleted { get; set; }
}

/// <summary>
///     A DAL carrying the denormalized search column the search repository bases maintain.
/// </summary>
public interface IDalSearchField
{
    /// <summary>
    ///     Normalized, space-separated search keywords for this row. Written by the repository, never by hand.
    /// </summary>
    public string? MetaDataSearch { get; set; }
}
