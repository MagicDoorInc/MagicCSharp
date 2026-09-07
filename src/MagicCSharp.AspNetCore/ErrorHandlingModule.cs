using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DomainNotFound = MagicCSharp.Infrastructure.Exceptions.NotFoundException;

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
        // Plain middleware rather than UseExceptionHandler, because that logs every exception it handles
        // at Error — including the 404s and 409s this module deliberately treats as routine, which makes
        // an error dashboard useless. Catching here means one log line, at the level the status implies.
        return app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                if (context.Response.HasStarted)
                {
                    // Too late to write a body: the status line is already on the wire. Log it and let the
                    // connection fail, rather than corrupting a half-sent response.
                    Logger(context).LogError(exception, "Exception after the response started on {Method} {Path}",
                        context.Request.Method, context.Request.Path);
                    throw;
                }

                await WriteProblem(context, exception);
            }
        });
    }

    private static async Task WriteProblem(HttpContext context, Exception exception)
    {
        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var problem = Describe(exception, environment.IsDevelopment());
        var logger = Logger(context);

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

        // The same id the X-Request-ID header carries, so a bug report quoting the body can be found in
        // the logs. TraceIdentifier is Kestrel's connection:request counter and does not match it.
        problem.Extensions["requestId"] = RequestIdOf(context);

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        // The content type goes to the write, not onto Response.ContentType first — WriteAsJsonAsync sets
        // application/json itself and would overwrite an earlier assignment, which is why these went out
        // mislabelled.
        await context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json");
    }

    private static ILogger Logger(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("MagicCSharp.ErrorHandling");
    }

    /// <summary>
    ///     The id this request is known by, from <see cref="IRequestIdHandler" /> — the same value the
    ///     X-Request-ID header carries and every log line for the request is tagged with, so a bug report
    ///     quoting the body can be found in the logs.
    ///     <para>
    ///         Not read from the response header: the middleware sets that in an OnStarting callback that
    ///         has not fired yet when this runs. Requires UseRequestId to sit outside this middleware, which
    ///         is how UseMagicApp orders them; Kestrel's own identifier is the fallback if it does not.
    ///     </para>
    /// </summary>
    private static string RequestIdOf(HttpContext context)
    {
        var current = context.RequestServices.GetService<IRequestIdHandler>()?.GetCurrentRequestId();

        return string.IsNullOrWhiteSpace(current) ? context.TraceIdentifier : current;
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
