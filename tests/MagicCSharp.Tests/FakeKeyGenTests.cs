using IdGen;
using MagicCSharp.Infrastructure.KeyGen;
using MagicCSharp.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MagicCSharp.Tests;

public class FakeKeyGenTests
{
    [Fact]
    public void Ids_follow_the_test_clock_rather_than_the_wall_clock()
    {
        // Without this, a test that sets its clock to 2019 gets ids stamped with today, and any assertion
        // about ordering passes or fails depending on when the suite runs.
        var timeProvider = new FakeTimeProvider();
        var fakeKeyGen = new FakeKeyGen(timeProvider);

        timeProvider.SetUtcNow(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var earlier = fakeKeyGen.GetId();

        timeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var later = fakeKeyGen.GetId();

        Assert.True(later > earlier);
    }

    [Fact]
    public void Two_generators_at_the_same_instant_do_not_collide()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var firstFakeKeyGen = new FakeKeyGen(timeProvider);
        var secondFakeKeyGen = new FakeKeyGen(timeProvider);

        Assert.NotEqual(firstFakeKeyGen.GetId(), secondFakeKeyGen.GetId());
    }
}
