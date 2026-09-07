using System.Reflection;
using MagicCSharp.AspNetCore;
using MagicCSharp.Events;
using MagicCSharp.Infrastructure.KeyGen;
using MagicCSharp.Modules;
using MagicCSharp.Scheduling;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MagicCSharp.App;

/// <summary>
///     Wires a MagicCSharp web service in two calls.
///     <para>
///         This is a shortcut, not a layer. Every method it calls is public on the package that owns it, so
///         when you outgrow the defaults you replace this with the four or five lines it stands for rather
///         than working around it. <see cref="MagicAppOptions" /> covers the common deviations.
///     </para>
/// </summary>
/// <example>
///     <code language="csharp">
///     var builder = WebApplication.CreateBuilder(args);
///     builder.AddMagicApp();
///
///     var app = builder.Build();
///     app.UseMagicApp();
///     app.Run();
///     </code>
/// </example>
public static class MagicAppSetup
{
    /// <summary>
    ///     Registers the framework's services: use cases, <c>IClock</c>, Snowflake IDs, request-ID tracking,
    ///     events, scheduling and error handling.
    /// </summary>
    public static WebApplicationBuilder AddMagicApp(this WebApplicationBuilder builder, MagicAppOptions? options = null)
    {
        builder.Services.AddMagicApp(options);
        return builder;
    }

    /// <inheritdoc cref="AddMagicApp(WebApplicationBuilder, MagicAppOptions?)" />
    public static IServiceCollection AddMagicApp(this IServiceCollection services, MagicAppOptions? options = null)
    {
        options ??= new MagicAppOptions();

        // Use cases, IClock, IRequestIdHandler.
        services.AddMagicCSharp(options.AssemblyFilter);

        // Snowflake IDs. Give each instance a distinct generator id in production, or two can issue the
        // same id in the same millisecond.
        services.AddSnowflakeKeyGen(options.KeyGeneratorId);

        if (options.Events)
        {
            // Handler discovery, then the transport. Local dispatch is registered only when nothing else
            // has claimed IEventDispatcher, so calling RegisterMagicKafkaEvents first works as expected.
            services.AddMagicEvents(options.OpenTelemetryMetrics);

            if (services.All(descriptor => descriptor.ServiceType != typeof(Events.Events.IEventDispatcher)))
            {
                services.AddLocalMagicEvents();
            }
        }

        if (options.Scheduling)
        {
            services.AddMagicScheduling(options.LockDirectory);
        }

        if (options.ErrorHandling)
        {
            services.AddMagicErrorHandling();
        }

        if (options.Controllers)
        {
            services.AddControllers();
        }

        return services;
    }

    /// <summary>
    ///     Builds the request pipeline: error handling first so it covers everything after it, then request
    ///     IDs, then controllers. Also runs the startup preflight.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <param name="builder">
    ///     The builder, needed only for the preflight — it is the service collection, not the provider, that
    ///     knows what was registered. Omit it to skip the check.
    /// </param>
    /// <param name="options">Must match what was passed to <see cref="AddMagicApp(WebApplicationBuilder, MagicAppOptions?)" />.</param>
    public static WebApplication UseMagicApp(this WebApplication app, WebApplicationBuilder? builder = null, MagicAppOptions? options = null)
    {
        options ??= new MagicAppOptions();

        // Before the first request rather than during it: a miswired dependency should fail the deploy.
        if (options.Preflight && builder != null)
        {
            app.Services.ValidateServices(builder.Services);
        }

        // Request id first, so it is established for everything after — including the error handler's own
        // log line and the requestId it puts in the problem body. With these the other way round the body
        // carried Kestrel's connection counter while the header carried the real id.
        app.UseRequestId();

        if (options.ErrorHandling)
        {
            app.UseMagicErrorHandling();
        }

        if (options.Controllers)
        {
            app.MapControllers();
        }

        return app;
    }
}

/// <summary>
///     What <see cref="MagicAppSetup" /> turns on. Everything defaults to on; switch a piece off when you
///     want to register it differently rather than not at all.
/// </summary>
public record MagicAppOptions
{
    /// <summary>
    ///     Register event handler discovery and, unless something else already claimed
    ///     <c>IEventDispatcher</c>, in-process dispatch. Turn off for a service that publishes nothing.
    /// </summary>
    public bool Events { get; init; } = true;

    /// <summary>
    ///     Use OpenTelemetry for event metrics rather than discarding them.
    /// </summary>
    public bool OpenTelemetryMetrics { get; init; }

    /// <summary>
    ///     Register the single-machine scheduling defaults. Turn off, or register your own store and lock
    ///     provider first, when running more than one instance.
    /// </summary>
    public bool Scheduling { get; init; } = true;

    /// <summary>Where the file-system lock provider keeps its lock files.</summary>
    public string? LockDirectory { get; init; }

    /// <summary>
    ///     Map exceptions to RFC 7807 problem responses. Turn off only if you are handling them yourself —
    ///     without it a domain <c>NotFoundException</c> reaches the caller as a 500.
    /// </summary>
    public bool ErrorHandling { get; init; } = true;

    /// <summary>Call <c>AddControllers</c> and <c>MapControllers</c>. Turn off for minimal APIs.</summary>
    public bool Controllers { get; init; } = true;

    /// <summary>
    ///     Resolve every registration at startup, so a miswired dependency fails the deploy rather than the
    ///     first request that needs it.
    /// </summary>
    public bool Preflight { get; init; } = true;

    /// <summary>
    ///     The Snowflake generator id, 0-1023. Leave null for a random one, which is fine on one instance
    ///     and a collision risk across many — give each a stable, distinct value in production.
    /// </summary>
    public int? KeyGeneratorId { get; init; }

    /// <summary>
    ///     Narrows which loaded assemblies are scanned for use cases. Defaults to every non-framework
    ///     assembly.
    /// </summary>
    public Func<Assembly, bool>? AssemblyFilter { get; init; }
}
