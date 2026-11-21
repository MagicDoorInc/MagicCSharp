namespace MagicCSharp.Clients.Tests;

public interface IProbeClient
{
    Task<TResponse> Get<TResponse>(string path);

    string PathWithQuery(string path, params object?[] values);
}

/// <summary>A client written on <see cref="MagicHttpClientBase" /> directly, to reach what generated clients call.</summary>
public class ProbeClient(IHttpClientFactory httpClientFactory) : MagicHttpClientBase(httpClientFactory), IProbeClient
{
    public async Task<TResponse> Get<TResponse>(string path)
    {
        return await SendRequestAsync<TResponse>(HttpMethod.Get, path, null, CancellationToken.None);
    }

    public string PathWithQuery(string path, params object?[] values)
    {
        return WithQuery(path, values);
    }
}
