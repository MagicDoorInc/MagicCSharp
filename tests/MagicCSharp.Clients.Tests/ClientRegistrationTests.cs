using MagicCSharp.Clients.Tests.Leases;
using MagicCSharp.Clients.Tests.Maintenance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Clients.Tests;

public class ClientRegistrationTests
{
    private const string LeaseJson = """{"id":7,"tenantName":"Ana Lee"}""";

    private readonly RecordingHttpMessageHandler recordingHttpMessageHandler = new RecordingHttpMessageHandler { AnswerBody = LeaseJson };

    [Fact]
    public async Task Registration_UsesTheConfiguredUrl()
    {
        // Arrange
        using var serviceProvider = ClientServices.Create(recordingHttpMessageHandler,
            new Dictionary<string, string?> { ["Services:Leasing:Url"] = "http://leasing.local" });
        var leasesClient = serviceProvider.GetRequiredService<ILeasesClient>();

        // Act
        await leasesClient.GetLease(7, 42);

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("leasing.local", recordedRequest.Host);
    }

    [Fact]
    public async Task Registration_FallsBackToTheDefaultUrl_WhenTheSettingIsMissing()
    {
        // Arrange
        using var serviceProvider = ClientServices.Create(recordingHttpMessageHandler, new Dictionary<string, string?>());
        var leasesClient = serviceProvider.GetRequiredService<ILeasesClient>();

        // Act
        await leasesClient.GetLease(7, 42);

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("leasing.test", recordedRequest.Host);
    }

    [Fact]
    public void Registration_Throws_NamingTheClient_WhenNoUrlIsSet()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var serviceCollection = new ServiceCollection();

        // Act
        var invalidOperationException = Assert.Throws<InvalidOperationException>(() => serviceCollection.AddMaintenanceClient(configuration));

        // Assert
        Assert.Equal("MaintenanceClient has no base URL: set the configuration value its [MagicClient] UrlSetting names, or give it a DefaultUrl.",
            invalidOperationException.Message);
    }

    [Fact]
    public async Task Registration_GivesTwoClientsWithTheSameShortName_EachItsOwnBaseAddress()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLeasesClient(configuration);
        Archive.LeasesClientRegistration.AddLeasesClient(serviceCollection, configuration);
        serviceCollection.AddHttpClient(typeof(Leases.LeasesClient).FullName!).ConfigurePrimaryHttpMessageHandler(() => recordingHttpMessageHandler);
        serviceCollection.AddHttpClient(typeof(Archive.LeasesClient).FullName!).ConfigurePrimaryHttpMessageHandler(() => recordingHttpMessageHandler);
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        // Act
        await serviceProvider.GetRequiredService<Leases.ILeasesClient>().GetLease(7, 42);
        recordingHttpMessageHandler.AnswerBody = "3";
        await serviceProvider.GetRequiredService<Archive.ILeasesClient>().GetArchivedLeaseCount();

        // Assert
        Assert.Equal(["leasing.test", "archive.test"], recordingHttpMessageHandler.Requests.Select(recordedRequest => recordedRequest.Host));
    }
}
