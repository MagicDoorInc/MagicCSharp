namespace MagicCSharp.Data.EntityFramework.Dals;

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
