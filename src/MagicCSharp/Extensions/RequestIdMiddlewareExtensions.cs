using MagicCSharp.Middleware;
using Microsoft.AspNetCore.Builder;

namespace MagicCSharp.Extensions;

/// <summary>
/// Extension methods for adding RequestId middleware to the application pipeline.
/// </summary>
public static class RequestIdMiddlewareExtensions
{
    /// <summary>
    /// Adds the RequestId middleware to the application pipeline.
    /// This middleware automatically generates or accepts a RequestId for each HTTP request,
    /// and adds it to response headers for client tracking.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestIdMiddleware>();
    }
}
