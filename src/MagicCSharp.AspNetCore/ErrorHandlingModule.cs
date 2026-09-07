using System.ComponentModel.DataAnnotations;
using MagicCSharp.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DomainNotFound = MagicCSharp.Infrastructure.Exceptions.NotFoundException;
using HttpNotFound = MagicCSharp.AspNetCore.NotFoundException;

namespace MagicCSharp.AspNetCore;

/// <summary>
///     Turns exceptions into RFC 7807 problem responses.
///     <para>
///         Without this, a repository throwing <see cref="DomainNotFound" /> for a row that is not there
///         becomes a 500 with a stack trace — the framework defines the exception and then leaves the caller
///         to discover it as a server error. This is the piece that makes "throw the domain exception" the
///         right thing for a use case to do.
///     </para>
/// </summary>
public static class ErrorHandlingModule
{
    /// <summary>
    ///     Adds problem-details error handling. Call <see cref="UseMagicErrorHandling" /> to put it in the
    ///     pipeline.
    /// </summary>
    public static IServiceCollection AddMagicErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        return services;
    }

    /// <summary>
    ///     Catches exceptions and writes a problem response. Put it first, so it covers everything after it.
    ///     <para>
    ///         Outside Development the detail of an unexpected exception is logged, not returned — an
    ///         unhandled exception's message routinely contains a connection string, a file path, or a row
    ///         someone should not see.
    ///     </para>
    /// </summary>
    public static IApplicationBuilder UseMagicErrorHandling(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

            if (feature?.Error is not { } exception)
            {
                return;
            }

            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            var problem = Describe(exception, environment.IsDevelopment());

            // Logged at the level the status implies: a 404 is routine, a 500 is not.
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("MagicCSharp.ErrorHandling");

            if (problem.Status >= 500)
            {
                logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                logger.LogInformation("{Status} on {Method} {Path}: {Message}",
                    problem.Status, context.Request.Method, context.Request.Path, exception.Message);
            }

            problem.Instance = context.Request.Path;
            problem.Extensions["requestId"] = context.TraceIdentifier;

            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(problem);
        }));
    }

    /// <summary>
    ///     Maps an exception to a problem. Public so a test can assert the mapping without a web host.
    /// </summary>
    public static ProblemDetails Describe(Exception exception, bool includeDetail)
    {
        switch (exception)
        {
            // Carries its own status, so it decides.
            case HttpException http:
                return new ProblemDetails
                {
                    Status = http.StatusCode,
                    Title = http.Title,
                    Detail = http.Message,
                };

            // The domain says "no such thing" without knowing about HTTP; here is where that becomes 404.
            case DomainNotFound notFound:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "NotFound",
                    Detail = notFound.Message,
                };

            case ValidationException validation:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "ValidationFailed",
                    Detail = validation.Message,
                };

            // A caller sent something the code refuses to work with. 400 rather than 500: the server is fine.
            case ArgumentException argument:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "BadRequest",
                    Detail = argument.Message,
                };

            // The client gave up, or the server is shutting down. Nothing to report and nobody listening.
            case OperationCanceledException:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status499ClientClosedRequest,
                    Title = "Cancelled",
                };

            default:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "InternalServerError",
                    // Only in Development: an unexpected exception's message routinely names a host, a path,
                    // or data the caller has no business seeing.
                    Detail = includeDetail ? exception.ToString() : "An unexpected error occurred.",
                };
        }
    }
}

file static class StatusCodes
{
    public const int Status400BadRequest = 400;
    public const int Status404NotFound = 404;

    /// <summary>Nginx's code for "the client hung up". Not in the BCL's list, but what proxies expect.</summary>
    public const int Status499ClientClosedRequest = 499;

    public const int Status500InternalServerError = 500;
}
