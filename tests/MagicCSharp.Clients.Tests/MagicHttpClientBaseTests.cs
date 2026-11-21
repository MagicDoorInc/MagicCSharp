using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Clients.Tests;

public class MagicHttpClientBaseTests : IDisposable
{
    private readonly RecordingHttpMessageHandler recordingHttpMessageHandler = new RecordingHttpMessageHandler();

    private readonly ServiceProvider serviceProvider;

    private readonly IProbeClient probeClient;

    public MagicHttpClientBaseTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddMagicHttpClient<IProbeClient, ProbeClient>(new ConfigurationBuilder().Build(), "http://probe.test");
        serviceCollection.AddHttpClient(typeof(ProbeClient).FullName!).ConfigurePrimaryHttpMessageHandler(() => recordingHttpMessageHandler);
        serviceProvider = serviceCollection.BuildServiceProvider();
        probeClient = serviceProvider.GetRequiredService<IProbeClient>();
    }

    public void Dispose()
    {
        serviceProvider.Dispose();
    }

    [Fact]
    public async Task Exception_KeepsTheResponseBodyOutOfTheMessage()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerStatus = HttpStatusCode.BadRequest;
        recordingHttpMessageHandler.AnswerBody = """{"error":"Ana Lee owes 4000"}""";

        // Act
        var magicHttpClientException = await Assert.ThrowsAsync<MagicHttpClientException>(() => probeClient.Get<string>("/api/leases/7"));

        // Assert
        Assert.Equal("GET /api/leases/7 answered 400 BadRequest.", magicHttpClientException.Message);
        Assert.Equal("""{"error":"Ana Lee owes 4000"}""", magicHttpClientException.ResponseBody);
    }

    [Fact]
    public async Task Exception_KeepsTheQueryStringOutOfTheMessage()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerStatus = HttpStatusCode.BadRequest;

        // Act
        var magicHttpClientException = await Assert.ThrowsAsync<MagicHttpClientException>(() => probeClient.Get<string>("/api/leases?email=ana%40lee.test&companyId=42"));

        // Assert
        Assert.Equal("GET /api/leases answered 400 BadRequest.", magicHttpClientException.Message);
    }

    [Fact]
    public async Task EmptyAnswer_ThrowsInvalidOperation_ForAValueTypeThatIsNotNullable()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerStatus = HttpStatusCode.NoContent;

        // Act
        var invalidOperationException = await Assert.ThrowsAsync<InvalidOperationException>(() => probeClient.Get<int>("/api/leases/count"));

        // Assert
        Assert.Equal("GET /api/leases/count answered with no content, which is not a Int32.", invalidOperationException.Message);
    }

    [Fact]
    public async Task EmptyAnswer_KeepsTheQueryStringOutOfTheMessage()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerStatus = HttpStatusCode.NoContent;

        // Act
        var invalidOperationException = await Assert.ThrowsAsync<InvalidOperationException>(() => probeClient.Get<int>("/api/leases/count?email=ana%40lee.test"));

        // Assert
        Assert.Equal("GET /api/leases/count answered with no content, which is not a Int32.", invalidOperationException.Message);
    }

    [Fact]
    public async Task EmptyAnswer_IsDefault_ForANullableValueType()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerStatus = HttpStatusCode.NoContent;

        // Act
        var count = await probeClient.Get<int?>("/api/leases/count");

        // Assert
        Assert.Null(count);
    }

    [Fact]
    public void WithQuery_Throws_ForAnObjectValuedProperty()
    {
        // Act
        var notSupportedException = Assert.Throws<NotSupportedException>(() => probeClient.PathWithQuery("/api/leases", new { Filter = new object() }));

        // Assert
        Assert.Contains(".Filter holds an object", notSupportedException.Message);
    }
}
