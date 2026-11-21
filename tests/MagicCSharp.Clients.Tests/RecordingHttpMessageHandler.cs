using System.Net;
using System.Text;

namespace MagicCSharp.Clients.Tests;

/// <summary>Stands in for the called service: records every request and answers with the scripted status and body.</summary>
public class RecordingHttpMessageHandler : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public HttpStatusCode AnswerStatus { get; set; } = HttpStatusCode.OK;

    public string AnswerBody { get; set; } = "";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // A real connection never sends a request whose token is already cancelled.
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest
        {
            Method = request.Method.Method,
            Host = request.RequestUri!.Host,
            PathAndQuery = request.RequestUri.PathAndQuery,
            Body = body,
        });

        var httpResponseMessage = new HttpResponseMessage(AnswerStatus);
        httpResponseMessage.Content = new StringContent(AnswerBody, Encoding.UTF8, "application/json");
        return httpResponseMessage;
    }
}
