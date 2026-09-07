using IdGen;
using MagicCSharp.Infrastructure.KeyGen;
using MagicCSharp.Testing;
using Xunit;

namespace MagicCSharp.Tests;

public class SnowflakeKeyGenServiceTests
{
    private readonly SnowflakeKeyGenService keyGen = new SnowflakeKeyGenService(new IdGenerator(1));

    [Fact]
    public void Ids_are_positive_so_the_sign_bit_stays_free()
    {
        var id = keyGen.GetId();

        Assert.True(id > 0);
        Assert.True(keyGen.IsValidId(id));
    }

    [Fact]
    public void Ids_increase_so_they_sort_by_creation_time()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => keyGen.GetId()).ToList();

        Assert.Equal(ids.OrderBy(id => id), ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Zero_and_negatives_are_not_valid_ids()
    {
        Assert.False(keyGen.IsValidId(0));
        Assert.False(keyGen.IsValidId(-1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(64)]
    public void Keys_are_the_requested_length(int length)
    {
        Assert.Equal(length, keyGen.GetKey(length).Length);
    }

    [Fact]
    public void Keys_avoid_the_characters_that_are_easy_to_confuse()
    {
        // 0/O and I/l are the pairs people get wrong reading a key off a screen or a printed page.
        var keys = string.Concat(Enumerable.Range(0, 200).Select(_ => keyGen.GetKey(32)));

        Assert.DoesNotContain('0', keys);
        Assert.DoesNotContain('O', keys);
        Assert.DoesNotContain('I', keys);
        Assert.DoesNotContain('l', keys);
    }

    [Fact]
    public void Generated_keys_validate()
    {
        Assert.True(keyGen.IsValidKey(keyGen.GetKey()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("has0zero")]
    [InlineData("has-hyphen")]
    public void Keys_outside_the_alphabet_do_not_validate(string candidate)
    {
        Assert.False(keyGen.IsValidKey(candidate));
    }

    [Fact]
    public void Keys_are_not_repeated()
    {
        var keys = Enumerable.Range(0, 1000).Select(_ => keyGen.GetKey()).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void A_zero_length_key_is_rejected_rather_than_returned_empty()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => keyGen.GetKey(0));
    }
}

public class FakeKeyGenTests
{
    [Fact]
    public void Ids_follow_the_test_clock_rather_than_the_wall_clock()
    {
        // Without this, a test that sets its clock to 2019 gets ids stamped with today, and any assertion
        // about ordering passes or fails depending on when the suite runs.
        var clock = new FakeClock();
        var keyGen = new FakeKeyGen(clock);

        clock.SetTime(2020, 1, 1);
        var earlier = keyGen.GetId();

        clock.SetTime(2026, 1, 1);
        var later = keyGen.GetId();

        Assert.True(later > earlier);
    }

    [Fact]
    public void Two_generators_at_the_same_instant_do_not_collide()
    {
        var clock = new FakeClock();
        clock.SetTime(2026, 1, 1);

        var first = new FakeKeyGen(clock);
        var second = new FakeKeyGen(clock);

        Assert.NotEqual(first.GetId(), second.GetId());
    }
}
