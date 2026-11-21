using System.Net;
using System.Text;
using MagicCSharp.Clients;
using MagicCSharp.Testing;

namespace PackageSmoke;

public record WidgetDto
{
    public required long Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>The generator writes <c>WidgetsClient</c> and <c>AddWidgetsClient</c>.</summary>
[MagicClient("api/widgets", DefaultUrl = "http://widgets.test")]
public interface IWidgetsClient
{
    [ClientGet("{widgetId}")]
    Task<WidgetDto?> GetWidget(long widgetId);
}

/// <summary>The generator implements every member of the interface as throwing <c>NotScriptedException</c>.</summary>
[GenerateFakeBase(typeof(IWidgetsClient))]
public abstract partial class FakeWidgetsClientBase;

public class FakeWidgetsClient : FakeWidgetsClientBase;

/// <summary>Answers every request with one widget, and keeps the address it was asked for.</summary>
public class StubWidgetsHandler : HttpMessageHandler
{
    public Uri? RequestedUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUri = request.RequestUri;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":7,\"name\":\"Lamp\"}", Encoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}
