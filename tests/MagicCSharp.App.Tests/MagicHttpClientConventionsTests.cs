using MagicCSharp.Clients;
using MagicCSharp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MagicCSharp.App.Tests;

/// <summary>
///     The default HTTP client of MagicCSharp.Clients against a MagicApp server set up with its defaults: both ends
///     read and write enums by name and <see cref="Optional{T}" /> as present, null or absent.
/// </summary>
public class MagicHttpClientConventionsTests : IAsyncLifetime
{
    private WebApplication app = null!;
    private ServiceProvider serviceProvider = null!;
    private ISerializationClient serializationClient = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging(logging => logging.ClearProviders());
        builder.AddMagicApp(new MagicAppOptions { ShouldRegisterScheduling = false, ShouldRunPreflight = false });
        builder.Services.AddControllers().AddApplicationPart(typeof(MagicHttpClientConventionsTests).Assembly);
        app = builder.Build();
        app.UseMagicApp();
        await app.StartAsync();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddMagicHttpClient<ISerializationClient, SerializationClient>(new ConfigurationBuilder().Build(), "http://magic-app.test");
        serviceCollection.AddHttpClient(MagicHttpClientRegistration.HttpClientName(typeof(SerializationClient)))
            .ConfigurePrimaryHttpMessageHandler(() => app.GetTestServer().CreateHandler());
        serviceProvider = serviceCollection.BuildServiceProvider();
        serializationClient = serviceProvider.GetRequiredService<ISerializationClient>();
    }

    public async Task DisposeAsync()
    {
        await serviceProvider.DisposeAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Client_ReadsAnEnumTheServerSendsByName()
    {
        var statusBody = await serializationClient.GetStatus();

        Assert.Equal(SampleStatus.Cancelled, statusBody.Status);
    }

    [Fact]
    public async Task Client_LeavesAnOptionalWithoutAValueOutOfTheBody()
    {
        var patchEcho = await serializationClient.Patch(new PatchBody { Name = "kept" });

        Assert.Equal("kept", patchEcho.Name);
        Assert.False(patchEcho.WasNicknameGiven);
    }

    [Fact]
    public async Task Client_SendsAnOptionalSetToNull_AsGivenAndNull()
    {
        var patchEcho = await serializationClient.Patch(new PatchBody { Name = "kept", Nickname = new Optional<string?>(null) });

        Assert.True(patchEcho.WasNicknameGiven);
        Assert.Null(patchEcho.Nickname);
    }

    [Fact]
    public async Task Client_SendsAnOptionalWithAValue_AsThatValue()
    {
        var patchEcho = await serializationClient.Patch(new PatchBody { Name = "kept", Nickname = "Babs" });

        Assert.True(patchEcho.WasNicknameGiven);
        Assert.Equal("Babs", patchEcho.Nickname);
    }
}
