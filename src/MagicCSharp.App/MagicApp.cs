using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace MagicCSharp.App;

/// <summary>
///     Starts a web service from its <see cref="MagicAppStartup" />, so <c>Main</c> is one call.
/// </summary>
/// <example>
///     <code language="csharp">
///     public class ShopProgram
///     {
///         public static void Main(string[] args)
///         {
///             MagicApp.Run&lt;ShopStartup&gt;(args);
///         }
///     }
///     </code>
/// </example>
public static class MagicApp
{
    /// <summary>
    ///     Builds the app, reports it started, and serves until it is stopped.
    ///     <para>
    ///         An exception on the way goes to <see cref="MagicAppStartup.OnFatal" /> — the inner one when it is an
    ///         <see cref="AggregateException" />, which hides the real cause — and is rethrown, so the process exits
    ///         with an error. A <see cref="HostAbortedException" /> is rethrown without it: a test host stops
    ///         start-up that way on purpose.
    ///     </para>
    /// </summary>
    public static void Run<TStartup>(string[] args)
        where TStartup : MagicAppStartup, new()
    {
        var startup = new TStartup();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var app = Build(startup, args);
            startup.OnStarted(app, stopwatch.Elapsed);
            app.Run();
        }
        catch (HostAbortedException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var aggregateException = exception as AggregateException;
            startup.OnFatal(aggregateException?.InnerException ?? exception);
            throw;
        }
    }

    /// <summary>
    ///     Builds the app ready to run: the framework's and the app's services, then the framework's pipeline (with
    ///     the preflight) and the app's.
    /// </summary>
    public static WebApplication Build<TStartup>(string[] args)
        where TStartup : MagicAppStartup, new()
    {
        return Build(new TStartup(), args);
    }

    private static WebApplication Build(MagicAppStartup startup, string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        startup.AddFrameworkServices(builder);
        startup.AddAppServices(builder.Services, builder.Configuration);

        var app = builder.Build();
        startup.UseFrameworkPipeline(app, builder);
        startup.UseAppPipeline(app);

        return app;
    }
}
