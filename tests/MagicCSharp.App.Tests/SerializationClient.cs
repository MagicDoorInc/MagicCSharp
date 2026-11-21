using System.Text.Json.Serialization;
using MagicCSharp.Clients;

namespace MagicCSharp.App.Tests;

public interface ISerializationClient
{
    Task<StatusBody> GetStatus();

    Task<PatchEcho> Patch(PatchBody patchBody);
}

/// <summary>The default client base calling <see cref="SerializationController" />, written by hand as a generated client would be.</summary>
public class SerializationClient(IHttpClientFactory httpClientFactory) : MagicHttpClientBase(httpClientFactory), ISerializationClient
{
    public async Task<StatusBody> GetStatus()
    {
        return await SendRequestAsync<StatusBody>(HttpMethod.Get, "serialization/status", null, CancellationToken.None);
    }

    public async Task<PatchEcho> Patch(PatchBody patchBody)
    {
        return await SendRequestAsync<PatchEcho>(HttpMethod.Post, "serialization/patch", patchBody, CancellationToken.None);
    }
}

/// <summary>What <see cref="SerializationController.Patch" /> says it received.</summary>
public record PatchEcho
{
    public string Name { get; init; } = "";

    [JsonPropertyName("nicknameWasGiven")]
    public bool WasNicknameGiven { get; init; }

    public string? Nickname { get; init; }
}
