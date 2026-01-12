using IdGen;

namespace MagicCSharp.Data.KeyGen;

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
}