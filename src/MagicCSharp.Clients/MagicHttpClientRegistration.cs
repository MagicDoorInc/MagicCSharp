using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Clients;

/// <summary>The registration generated clients call when no <see cref="MagicClientConventionAttribute" /> names another.</summary>
public static class MagicHttpClientRegistration
{
    /// <summary>
    ///     Registers <typeparamref name="TClient" /> as the singleton <typeparamref name="TInterface" />, with a named
    ///     <see cref="HttpClient" /> for it whose base address is <paramref name="baseUrl" />.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="configuration">Unused here; part of the registration shape so a convention's registration can read its own settings.</param>
    /// <param name="baseUrl">The service's base URL, from the client's <c>UrlSetting</c> or <c>DefaultUrl</c>.</param>
    public static IServiceCollection AddMagicHttpClient<TInterface, TClient>(this IServiceCollection services, IConfiguration configuration, string? baseUrl)
        where TInterface : class
        where TClient : class, TInterface
    {
        if (baseUrl == null)
        {
            throw new InvalidOperationException(
                $"{typeof(TClient).Name} has no base URL: set the configuration value its [MagicClient] UrlSetting names, or give it a DefaultUrl.");
        }

        // Without a trailing slash a relative path replaces the base URL's last segment: http://host/api plus
        // orders would be http://host/orders.
        var baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
        services.AddHttpClient(typeof(TClient).Name, httpClient =>
        {
            httpClient.BaseAddress = baseUri;
        });
        services.AddSingleton<TInterface, TClient>();
        return services;
    }
}
