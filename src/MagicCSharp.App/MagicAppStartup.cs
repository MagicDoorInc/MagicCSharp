using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.App;

/// <summary>
///     An application's setup class: what <see cref="MagicApp.Run{TStartup}" /> registers and maps, each step a
///     method to override.
///     <para>
///         An app writes <see cref="AddAppServices" /> — its repositories, clients and modules — and, when it has
///         them, its own endpoints in <see cref="UseAppPipeline" />. The framework's part defaults to
///         <see cref="MagicAppSetup" />'s <c>AddMagicApp</c> and <c>UseMagicApp</c> with <see cref="Options" />; a
///         codebase with its own hosting overrides <see cref="AddFrameworkServices" /> and
///         <see cref="UseFrameworkPipeline" /> once, in a base class its apps share.
///     </para>
/// </summary>
/// <example>
///     <code language="csharp">
///     public class ShopStartup : MagicAppStartup
///     {
///         protected override void AddAppServices(IServiceCollection services, IConfiguration configuration)
///         {
///             services.AddShopRepositories(configuration);
///         }
///     }
///     </code>
/// </example>
public abstract class MagicAppStartup
{
    /// <summary>What the framework defaults turn on. Set it in the subclass's constructor to change any of it.</summary>
    public MagicAppOptions Options { get; protected init; } = new MagicAppOptions();

    /// <summary>The framework's services. Defaults to <c>AddMagicApp</c> with <see cref="Options" />.</summary>
    protected internal virtual void AddFrameworkServices(WebApplicationBuilder builder)
    {
        builder.AddMagicApp(Options);
    }

    /// <summary>The app's own services: its repositories, clients and modules.</summary>
    protected internal abstract void AddAppServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    ///     The framework's request pipeline and the startup preflight. Defaults to <c>UseMagicApp</c> with
    ///     <see cref="Options" />.
    /// </summary>
    protected internal virtual void UseFrameworkPipeline(WebApplication app, WebApplicationBuilder builder)
    {
        app.UseMagicApp(builder, Options);
    }

    /// <summary>The app's own endpoints and middleware, mapped after the framework's. Nothing by default.</summary>
    protected internal virtual void UseAppPipeline(WebApplication app)
    {
    }

    /// <summary>Runs once the app is built and its preflight passed, just before it starts serving.</summary>
    protected internal virtual void OnStarted(WebApplication app, TimeSpan startupTime)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MagicCSharp.App");
        logger.LogInformation("Application started in {ElapsedMilliseconds}ms", (long)startupTime.TotalMilliseconds);
    }

    /// <summary>
    ///     Receives the exception that stopped the app, before it is rethrown. No logger exists yet when building
    ///     fails, so the default writes it to standard error.
    /// </summary>
    protected internal virtual void OnFatal(Exception exception)
    {
        Console.Error.WriteLine($"Application terminated unexpectedly: {exception}");
    }
}
