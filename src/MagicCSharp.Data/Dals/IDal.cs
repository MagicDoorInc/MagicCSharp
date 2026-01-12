namespace MagicCSharp.Data.Dals;

/// <summary>
///     Base interface for all Data Access Layer (DAL) objects with timestamp tracking.
/// </summary>
public interface IDal
{
    /// <summary>
    ///     When the DAL object was created.
    /// </summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    ///     When the DAL object was last updated.
    /// </summary>
    public DateTimeOffset Updated { get; set; }
}