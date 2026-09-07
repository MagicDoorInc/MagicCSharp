using System.Buffers;
using System.Security.Cryptography;
using IdGen;

namespace MagicCSharp.Infrastructure.KeyGen;

/// <summary>
///     Snowflake ID generator service using Twitter's Snowflake algorithm.
///     <para>
///         Generates unique 64-bit IDs composed of:
///         - 41 bits for time in milliseconds (provides 69 years with custom epoch)
///         - 10 bits for generator ID (supports up to 1024 generators)
///         - 12 bits for sequence number (allows up to 4096 IDs per millisecond)
///         - 1 unused sign bit
///     </para>
///     <para>
///         IDs are sortable by time, making them ideal for database primary keys.
///     </para>
/// </summary>
public class SnowflakeKeyGenService : IKeyGenService
{
    /// <summary>
    ///     Base58: the digits and letters minus the four that are easy to confuse when a key is read aloud,
    ///     handwritten, or typed from a screenshot — zero and capital O, capital I and lowercase l.
    /// </summary>
    private const string FriendlyAlphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    private static readonly SearchValues<char> FriendlyCharacters = SearchValues.Create(FriendlyAlphabet);

    private readonly IdGenerator generator;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SnowflakeKeyGenService" /> class.
    /// </summary>
    /// <param name="generator">The IdGen generator instance.</param>
    public SnowflakeKeyGenService(IdGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        this.generator = generator;
    }

    /// <inheritdoc />
    public long GetId()
    {
        return generator.CreateId();
    }

    /// <inheritdoc />
    public bool IsValidId(long input)
    {
        // The sign bit is unused, so every id this generator produces is positive.
        return input > 0;
    }

    /// <inheritdoc />
    public string GetKey(int length = 16)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

        return string.Create(length, FriendlyAlphabet, (span, alphabet) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                // GetInt32 rejection-samples, so each character is equally likely. A plain modulo of a random
                // byte would bias the first 24 characters of a 58-character alphabet.
                span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });
    }

    /// <inheritdoc />
    public bool IsValidKey(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        return input.AsSpan().IndexOfAnyExcept(FriendlyCharacters) == -1;
    }
}
