using System.Net.Http.Json;
using System.Text.Json;
using MagicCSharp.App;
using MagicCSharp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MagicCSharp.App.Tests;

/// <summary>
///     What the API puts on the wire, asserted against a running host.
///     <para>
///         The framework has one notion of how its types serialize, in <see cref="JsonDefaults" />, and it
///         was applied to Postgres jsonb columns and nowhere else. So the same enum was a name in the
///         database and a number over HTTP, and <see cref="Optional{T}" /> — which exists so a PATCH can
///         tell "set this to null" apart from "do not touch this" — did not round-trip through a request
///         body at all.
///     </para>
/// </summary>
public class ApiSerializationTests : IAsyncLifetime
{
    private WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging(logging => logging.ClearProviders());

        builder.AddMagicApp(new MagicAppOptions { Scheduling = false, Preflight = false });
        builder.Services.AddControllers().AddApplicationPart(typeof(ApiSerializationTests).Assembly);

        app = builder.Build();
        app.UseMagicApp();

        await app.StartAsync();
        client = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task An_enum_goes_out_as_its_name()
    {
        // The database stores enums by name, so that adding a member does not change what existing rows
        // mean. A client hard-coding 2 has exactly the same problem, so the API agrees.
        var body = await client.GetStringAsync("/serialization/status");

        Assert.Contains("\"Cancelled\"", body);
        Assert.DoesNotContain("\"status\":2", body);
    }

    [Fact]
    public async Task An_enum_name_is_accepted_on_the_way_in()
    {
        var response = await client.PostAsJsonAsync("/serialization/status", new { status = "Cancelled" });

        response.EnsureSuccessStatusCode();
        Assert.Contains("Cancelled", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_optional_the_caller_did_not_mention_is_absent()
    {
        var response = await client.PostAsJsonAsync("/serialization/patch", new { name = "kept" });
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal("kept", result.GetProperty("name").GetString());
        Assert.False(result.GetProperty("nicknameWasGiven").GetBoolean());
    }

    [Fact]
    public async Task An_optional_explicitly_set_to_null_is_distinguishable_from_absent()
    {
        // This distinction is the entire reason Optional exists. Without the converter both cases arrive
        // as "no value" and a PATCH cannot clear a field.
        var response = await client.PostAsJsonAsync("/serialization/patch",
            new { name = "kept", nickname = (string?)null });

        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        Assert.True(result.GetProperty("nicknameWasGiven").GetBoolean());
        Assert.Null(result.GetProperty("nickname").GetString());
    }

    [Fact]
    public async Task An_optional_with_a_value_arrives_with_it()
    {
        var response = await client.PostAsJsonAsync("/serialization/patch",
            new { name = "kept", nickname = "Babs" });

        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

        Assert.True(result.GetProperty("nicknameWasGiven").GetBoolean());
        Assert.Equal("Babs", result.GetProperty("nickname").GetString());
    }

    [Fact]
    public async Task A_computed_property_is_still_serialized()
    {
        // JsonDefaults sets IgnoreReadOnlyProperties, which is right for a jsonb column and wrong for a
        // response body: it would silently drop Pagination.TotalPages and every other computed field. So
        // the converters are applied, not the whole options object.
        var body = await client.GetStringAsync("/serialization/computed");

        Assert.Contains("doubled", body);
    }
}

public enum SampleStatus
{
    Pending,
    Paid,
    Cancelled,
}

public record StatusBody
{
    public SampleStatus Status { get; init; }
}

public record PatchBody
{
    public string Name { get; init; } = "";
    public Optional<string?> Nickname { get; init; }
}

public record ComputedBody
{
    public int Value { get; init; }
    public int Doubled => Value * 2;
}

[ApiController]
[Route("serialization")]
public class SerializationController : ControllerBase
{
    [HttpGet("status")]
    public StatusBody GetStatus() => new StatusBody { Status = SampleStatus.Cancelled };

    [HttpPost("status")]
    public StatusBody PostStatus([FromBody] StatusBody body) => body;

    [HttpPost("patch")]
    public object Patch([FromBody] PatchBody body)
    {
        return new
        {
            name = body.Name,
            nicknameWasGiven = body.Nickname.HasValue,
            nickname = body.Nickname.HasValue ? body.Nickname.Value : null,
        };
    }

    [HttpGet("computed")]
    public ComputedBody GetComputed() => new ComputedBody { Value = 21 };
}
