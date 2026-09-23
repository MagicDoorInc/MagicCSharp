namespace MagicCSharp.Infrastructure.KeyGen;

/// <summary>
///     Generates the two identifier shapes the repositories support: a numeric, time-sortable id for entities
///     keyed by <c>long</c>, and an unguessable string key for entities keyed by <c>string</c>.
/// </summary>
public interface IKeyGenService
{
    /// <summary>
    ///     Generate a distributed-unique, time-sortable 64-bit id. Safe to use as a database primary key across
    ///     instances without coordination.
    /// </summary>
    long GetId();

    /// <summary>
    ///     Whether the value has the shape of an id this service produces. Cheap syntactic check for rejecting
    ///     obviously invalid route and query values before touching the database; it does not prove the id exists.
    /// </summary>
    bool IsValidId(long input);

    /// <summary>
    ///     Generate an unpredictable string key of <paramref name="length" /> characters.
    ///     <para>
    ///         Unlike <see cref="GetId" /> the result carries no timestamp and reveals nothing about how many have
    ///         been issued, which is what makes it safe to put in a URL a user can see or share.
    ///     </para>
    /// </summary>
    /// <param name="length">Number of characters. 16 gives roughly 94 bits of entropy over the 58-character alphabet.</param>
    string GetKey(int length = 16);

    /// <summary>
    ///     Whether every character of <paramref name="input" /> is in the alphabet <see cref="GetKey" /> draws from.
    ///     Cheap syntactic check; it does not prove the key exists.
    /// </summary>
    bool IsValidKey(string input);
}
