using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.KeyGen;

namespace MagicCSharp.Testing;

/// <summary>
///     An <see cref="IKeyGenService" /> whose ids are derived from a <see cref="FakeClock" /> rather than the
///     wall clock.
///     <para>
///         A Snowflake id encodes the time it was issued. Generating one from the real clock inside a test that
///         has set its clock to a date in 2019 produces an id that sorts after everything the test then creates —
///         so ordering assertions pass or fail depending on when the suite runs. Reading the time from the same
///         clock the test controls keeps ids and timestamps telling the same story.
///     </para>
///     <para>
///         Ids are unique within a process but not cryptographically random, and keys use the same alphabet as
///         the real generator. Neither is suitable outside tests.
///     </para>
/// </summary>
/// <param name="clock">The clock the test controls.</param>
/// <param name="generatorId">
///     Distinguishes ids from two generators issued in the same millisecond. Defaults to a per-instance value, so
///     two <see cref="FakeKeyGen" /> instances in one process — "two nodes" in a test — do not collide.
/// </param>
public class FakeKeyGen(IClock clock, int? generatorId = null) : IKeyGenService
{
    private const string FriendlyAlphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    /// <summary>Matches the epoch the production generator uses, so ids are comparable across the two.</summary>
    private static readonly DateTimeOffset Epoch = new DateTimeOffset(2015, 1, 1, 0,
        0, 0, TimeSpan.Zero);

    private static int globalGeneratorCounter;
    private static int globalSequenceCounter;

    private readonly long instanceGeneratorId = (generatorId ?? Interlocked.Increment(ref globalGeneratorCounter)) & 0x3FF;

    public long GetId()
    {
        var timestamp = (long)(clock.Now() - Epoch).TotalMilliseconds & 0x1FFFFFFFFFFL;
        var sequence = (uint)Interlocked.Increment(ref globalSequenceCounter) & 0xFFF;

        // {1-bit sign (0)}{41-bit timestamp}{10-bit generator}{12-bit sequence}
        return (timestamp << 22) | (instanceGeneratorId << 12) | sequence;
    }

    public bool IsValidId(long input)
    {
        return input > 0;
    }

    public string GetKey(int length = 16)
    {
        return string.Create(length, FriendlyAlphabet, (span, alphabet) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = alphabet[Random.Shared.Next(alphabet.Length)];
            }
        });
    }

    public bool IsValidKey(string input)
    {
        return !string.IsNullOrEmpty(input) && input.All(FriendlyAlphabet.Contains);
    }
}
