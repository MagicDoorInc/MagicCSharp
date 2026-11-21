using System.Net;

namespace MagicCSharp.Clients;

/// <summary>
///     A service answered a <see cref="MagicHttpClientBase" /> request with an error status. The answer's body is
///     kept in <see cref="ResponseBody" /> and left out of the message, so logging the exception does not log what
///     the service sent back.
/// </summary>
public class MagicHttpClientException(HttpMethod method, string path, HttpStatusCode statusCode, string responseBody)
    : HttpRequestException($"{method} {path} answered {(int)statusCode} {statusCode}.", null, statusCode)
{
    public string ResponseBody { get; } = responseBody;
}
