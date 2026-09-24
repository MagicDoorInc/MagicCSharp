using MagicCSharp.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace MagicCSharp.AspNetCore;

/// <summary>
///     Middleware that automatically sets a RequestId for each HTTP request.
///     Accepts X-Request-ID header from clients, or generates a new ID if not provided.
///     Adds the RequestId to response headers for client tracking.
/// </summary>
public class RequestIdMiddleware
{
    /// <summary>The header this middleware reads and writes.</summary>
    public const string HeaderName = "X-Request-ID";

    /// <summary>
    ///     A caller-supplied id is echoed back, but only if it is short and printable — it ends up in every
    ///     log line for the request, and an unbounded header is a cheap way to flood a log or smuggle
    ///     control characters into one.
    /// </summary>
    private const int MaxRequestIdLength = 128;
    private readonly RequestDelegate _next;
    private readonly IRequestIdHandler _requestIdHandler;

    public RequestIdMiddleware(RequestDelegate next, IRequestIdHandler requestIdHandler)
    {
        _next = next;
        _requestIdHandler = requestIdHandler;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check for existing RequestId in header, or generate new one
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var requestId = IsAcceptable(supplied) ? supplied : null;

        if (string.IsNullOrWhiteSpace(requestId))
        {
            // Generate new RequestId if not provided by client
            requestId = Guid.NewGuid().ToString().Split('-').First();
        }

        using (_requestIdHandler.SetRequestId(requestId))
        {
            // Add RequestId to response headers for client tracking
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(HeaderName))
                {
                    context.Response.Headers[HeaderName] = requestId;
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }

    private static bool IsAcceptable(string? candidate)
    {
        return !string.IsNullOrWhiteSpace(candidate)
               && candidate.Length <= MaxRequestIdLength
               && candidate.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
    }
}
