using System.Text.Json;
using MagicCSharp.Infrastructure;
using Xunit;

namespace MagicCSharp.Tests;

public class OptionalTests
{
    private record PatchRequest
    {
        public Optional<string?> NickName { get; init; }
        public Optional<int> Age { get; init; }
    }

    [Fact]
    public void An_omitted_property_is_absent()
    {
        var request = JsonSerializer.Deserialize<PatchRequest>("""{"age":40}""", JsonDefaults.Options)!;

        Assert.False(request.NickName.HasValue);
    }

    [Fact]
    public void A_property_sent_as_null_is_present_and_null()
    {
        // This is the whole point of the type: "clear the nickname" and "leave it alone" are different
        // instructions that a plain string? cannot tell apart.
        var request = JsonSerializer.Deserialize<PatchRequest>("""{"nickName":null}""", JsonDefaults.Options)!;

        Assert.True(request.NickName.HasValue);
        Assert.Null(request.NickName.Value);
    }

    [Fact]
    public void A_property_sent_with_a_value_is_present_with_it()
    {
        var request = JsonSerializer.Deserialize<PatchRequest>("""{"nickName":"Babs"}""", JsonDefaults.Options)!;

        Assert.True(request.NickName.HasValue);
        Assert.Equal("Babs", request.NickName.Value);
    }

    [Fact]
    public void Absent_properties_are_left_out_of_the_output_entirely()
    {
        // The converter writes nothing for an absent value, so without the ShouldSerialize hook in
        // JsonDefaults this produces a property name followed by nothing — malformed JSON.
        var json = JsonSerializer.Serialize(new PatchRequest { Age = 40 }, JsonDefaults.Options);

        Assert.Equal("""{"Age":40}""", json);

        // Parses, which is the assertion that actually matters.
        using var parsed = JsonDocument.Parse(json);
        Assert.False(parsed.RootElement.TryGetProperty("NickName", out _));
    }

    [Fact]
    public void Round_trips_through_serialization()
    {
        var original = new PatchRequest { NickName = "Babs" };

        var json = JsonSerializer.Serialize(original, JsonDefaults.Options);
        var restored = JsonSerializer.Deserialize<PatchRequest>(json, JsonDefaults.Options)!;

        Assert.True(restored.NickName.HasValue);
        Assert.Equal("Babs", restored.NickName.Value);
        Assert.False(restored.Age.HasValue);
    }

    [Fact]
    public void OrElse_falls_back_only_when_absent()
    {
        Assert.Equal("fallback", default(Optional<string>).OrElse("fallback"));
        Assert.Equal("set", new Optional<string>("set").OrElse("fallback"));
    }

    [Fact]
    public void A_bare_value_converts_implicitly()
    {
        Optional<int> value = 7;

        Assert.True(value.HasValue);
        Assert.Equal(7, value.Value);
    }
}
