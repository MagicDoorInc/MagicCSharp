namespace MagicCSharp.Data.Dals;

/// <summary>
/// Interface for DAL objects that use a string key as their primary identifier.
/// </summary>
public interface IDalKey
{
    /// <summary>
    /// The string key that uniquely identifies this DAL object.
    /// </summary>
    string Key { get; set; }
}
