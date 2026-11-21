using MagicCSharp.Clients.Tests.Leases;
using MagicCSharp.Clients.Tests.Units;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Clients.Tests;

/// <summary>
///     The test clients registered the way an application registers them, through their generated <c>Add…</c>
///     methods, with the service they call replaced by a <see cref="RecordingHttpMessageHandler" />.
/// </summary>
public static class ClientServices
{
    public static ServiceProvider Create(RecordingHttpMessageHandler recordingHttpMessageHandler, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new UnitLabelSettings { Prefix = "Unit " });
        serviceCollection.AddLeasesClient(configuration);
        serviceCollection.AddUnitsClient(configuration);
        serviceCollection.AddHttpClient(typeof(LeasesClient).FullName!).ConfigurePrimaryHttpMessageHandler(() => recordingHttpMessageHandler);
        serviceCollection.AddHttpClient(typeof(UnitsClient).FullName!).ConfigurePrimaryHttpMessageHandler(() => recordingHttpMessageHandler);
        return serviceCollection.BuildServiceProvider();
    }

    public static ServiceProvider Create(RecordingHttpMessageHandler recordingHttpMessageHandler)
    {
        return Create(recordingHttpMessageHandler, new Dictionary<string, string?> { ["Services:Leasing:Url"] = "http://leasing.local" });
    }
}
