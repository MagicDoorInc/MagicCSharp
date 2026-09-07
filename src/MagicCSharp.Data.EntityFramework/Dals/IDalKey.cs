namespace MagicCSharp.Data.EntityFramework.Dals;

/// <summary>
///     Interface for DAL objects that use a string key as their primary identifier.
/// </summary>
public interface IDalKey
{
    /// <summary>
    ///     The string key that uniquely identifies this DAL object.
    ///     <para>
    ///         Read-only here, matching <see cref="IDalId" />: a key is assigned once, when the row is created, by
    ///         the DAL's own factory. The concrete DAL declares the setter it needs for that.
    ///     </para>
    /// </summary>
    string Key { get; }
}