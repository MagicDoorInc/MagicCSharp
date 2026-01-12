using MagicCSharp.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace MagicCSharp.Middleware;

/// <summary>
///     Middleware that automatically sets a RequestId for each HTTP request.
///     Accepts X-Request-ID header from clients, or generates a new ID if not provided.
///     Adds the RequestId to response headers for client tracking.
/// </summary>
public class RequestIdMiddleware
{
    private const string RequestIdHeaderName = "X-Request-ID";
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
        var requestId = context.Request.Headers[RequestIdHeaderName].FirstOrDefault();

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
                if (!context.Response.Headers.ContainsKey(RequestIdHeaderName))
                {
                    context.Response.Headers[RequestIdHeaderName] = requestId;
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}