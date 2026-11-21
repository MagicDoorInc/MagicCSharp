using System.Net;
using MagicCSharp.Clients.Tests.Leases;
using MagicCSharp.Clients.Tests.Units;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Clients.Tests;

public class GeneratedClientTests : IDisposable
{
    private const string LeaseJson = """{"id":7,"tenantName":"Ana Lee"}""";

    private readonly RecordingHttpMessageHandler recordingHttpMessageHandler = new RecordingHttpMessageHandler();

    private readonly ServiceProvider serviceProvider;

    private readonly ILeasesClient leasesClient;

    public GeneratedClientTests()
    {
        serviceProvider = ClientServices.Create(recordingHttpMessageHandler);
        leasesClient = serviceProvider.GetRequiredService<ILeasesClient>();
    }

    public void Dispose()
    {
        serviceProvider.Dispose();
    }

    [Fact]
    public async Task GeneratedClient_PutsRouteValuesInThePath_Escaped()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerBody = LeaseJson;

        // Act
        await leasesClient.GetLeaseByTenant("Ana Lee/2");

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("GET", recordedRequest.Method);
        Assert.Equal("/api/leases/by-tenant/Ana%20Lee%2F2", recordedRequest.PathAndQuery);
    }

    [Fact]
    public async Task GeneratedClient_SendsScalarParametersAsTheQuery()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerBody = LeaseJson;

        // Act
        await leasesClient.GetLease(7, 42);

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("/api/leases/7?companyId=42", recordedRequest.PathAndQuery);
    }

    [Fact]
    public async Task GeneratedClient_SendsObjectParametersAsTheQuery_OnGet()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerBody = "[" + LeaseJson + "]";

        // Act
        await leasesClient.GetLeases(new LeaseFilter { LeaseIds = [3, 4], Status = "active" }, new PageRequest { Page = 2, PageSize = 10 });

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("GET", recordedRequest.Method);
        Assert.Equal("/api/leases?LeaseIds=3&LeaseIds=4&Status=active&Page=2&PageSize=10", recordedRequest.PathAndQuery);
        Assert.Null(recordedRequest.Body);
    }

    [Fact]
    public async Task GeneratedClient_SendsTheObjectParameterAsTheBody_OnPost()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerBody = LeaseJson;

        // Act
        await leasesClient.CreateLease(new CreateLeaseRequest { TenantName = "Ana Lee", MonthlyRent = 1200 });

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("POST", recordedRequest.Method);
        Assert.Equal("/api/leases", recordedRequest.PathAndQuery);
        Assert.Equal("""{"tenantName":"Ana Lee","monthlyRent":1200}""", recordedRequest.Body);
    }

    [Fact]
    public async Task GeneratedClient_SendsTheObjectParameterAsTheBody_OnPut()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerBody = LeaseJson;

        // Act
        await leasesClient.UpdateLease(7, new UpdateLeaseRequest { TenantName = "Ana Lee-Park" });

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("PUT", recordedRequest.Method);
        Assert.Equal("/api/leases/7", recordedRequest.PathAndQuery);
        Assert.Equal("""{"tenantName":"Ana Lee-Park"}""", recordedRequest.Body);
    }

    [Fact]
    public async Task GeneratedClient_SendsTheObjectParameterAsTheBody_OnPatch()
    {
        // Act
        await leasesClient.ChangeRent(7, new ChangeRentRequest { MonthlyRent = 1350 });

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("PATCH", recordedRequest.Method);
        Assert.Equal("/api/leases/7/rent", recordedRequest.PathAndQuery);
        Assert.Equal("""{"monthlyRent":1350}""", recordedRequest.Body);
    }

    [Fact]
    public async Task GeneratedClient_SendsTheObjectParameterAsTheBody_OnDelete()
    {
        // Act
        await leasesClient.DeleteNotes(7, new DeleteNotesRequest { NoteIds = [11, 12] });

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("DELETE", recordedRequest.Method);
        Assert.Equal("/api/leases/7/notes", recordedRequest.PathAndQuery);
        Assert.Equal("""{"noteIds":[11,12]}""", recordedRequest.Body);
    }

    [Fact]
    public async Task GeneratedClient_SendsAnEmptyJsonObject_WhenAPutHasNoObjectParameter()
    {
        // Act
        await leasesClient.MarkRenewalReminderSent(7);

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("PUT", recordedRequest.Method);
        Assert.Equal("/api/leases/7/renewal-reminder-sent", recordedRequest.PathAndQuery);
        Assert.Equal("{}", recordedRequest.Body);
    }

    [Fact]
    public async Task GeneratedClient_SendsNoBody_WhenADeleteHasNoObjectParameter()
    {
        // Act
        await leasesClient.DeleteLease(7);

        // Assert
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("DELETE", recordedRequest.Method);
        Assert.Equal("/api/leases/7", recordedRequest.PathAndQuery);
        Assert.Null(recordedRequest.Body);
    }

    [Fact]
    public async Task GeneratedClient_ReturnsNull_ForANullableResult_WhenTheServerAnswers404()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerStatus = HttpStatusCode.NotFound;

        // Act
        var leaseDto = await leasesClient.GetLease(7, 42);

        // Assert
        Assert.Null(leaseDto);
        Assert.Single(recordingHttpMessageHandler.Requests);
    }

    [Fact]
    public async Task GeneratedClient_ReturnsNull_ForANullableResult_WhenTheServerAnswers204()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerStatus = HttpStatusCode.NoContent;

        // Act
        var leaseDto = await leasesClient.GetLease(7, 42);

        // Assert
        Assert.Null(leaseDto);
        Assert.Single(recordingHttpMessageHandler.Requests);
    }

    [Fact]
    public async Task GeneratedClient_Throws_ForANonNullableResult_WhenTheServerAnswers404()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerStatus = HttpStatusCode.NotFound;

        // Act
        var magicHttpClientException = await Assert.ThrowsAsync<MagicHttpClientException>(() => leasesClient.GetLeaseByTenant("Ana Lee"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, magicHttpClientException.StatusCode);
    }

    [Fact]
    public async Task GeneratedClient_CancelsTheRequest_WhenTheMethodsTokenIsCancelled()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerBody = "125.50";
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leasesClient.GetBalance(7, cancellationTokenSource.Token));

        // Assert
        Assert.Empty(recordingHttpMessageHandler.Requests);
    }

    [Fact]
    public async Task GeneratedClient_RunsTheHandWrittenMember_FromThePartialClass()
    {
        // Act
        var leaseDtos = await leasesClient.GetLeases([]);

        // Assert
        Assert.Empty(leaseDtos);
        Assert.Empty(recordingHttpMessageHandler.Requests);
    }

    [Fact]
    public async Task GeneratedClient_RunsTheHandWrittenBody_OfAMemberThatKeepsItsRouteAttribute()
    {
        // Act
        var documentNames = await leasesClient.GetDocumentNames(7);

        // Assert
        Assert.Equal(["lease-7.pdf"], documentNames);
        Assert.Empty(recordingHttpMessageHandler.Requests);
    }

    [Fact]
    public async Task GeneratedClient_UsesTheHandWrittenConstructor_WhenThePartialClassDeclaresOne()
    {
        // Arrange
        recordingHttpMessageHandler.AnswerBody = "\"4B\"";
        var unitsClient = serviceProvider.GetRequiredService<IUnitsClient>();

        // Act
        var unitLabel = await unitsClient.GetUnitLabel(12);

        // Assert
        Assert.Equal("Unit 4B", unitLabel);
        var recordedRequest = Assert.Single(recordingHttpMessageHandler.Requests);
        Assert.Equal("/api/units/12/name", recordedRequest.PathAndQuery);
    }
}
