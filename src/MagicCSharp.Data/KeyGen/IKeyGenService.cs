namespace MagicCSharp.Data.KeyGen;

/// <summary>
///     Service for generating unique identifiers.
/// </summary>
public interface IKeyGenService
{
    /// <summary>
    ///     Generate a unique ID.
    /// </summary>
    /// <returns>A unique 64-bit integer ID.</returns>
    long GetId();
}