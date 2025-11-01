namespace MagicCSharp.Data.Dals;

/// <summary>
/// Interface for DAL objects that use a numeric ID as their primary identifier.
/// </summary>
public interface IDalId
{
    /// <summary>
    /// The numeric ID that uniquely identifies this DAL object.
    /// </summary>
    public long Id { get; }
}
