using System.Net;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.App.Tests;

/// <summary>
///     <see cref="MagicApp" /> building and running a service from its <see cref="MagicAppStartup" />, on a test server.
/// </summary>
public class MagicAppTests
{
    [Fact]
    public async Task Build_ServesTheStartupsEndpointThroughTheFrameworkPipeline()
    {
        // Arrange
        await using var app = MagicApp.Build<BoilersStartup>([]);
        await app.StartAsync();
        using var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/boilers/7");

        // Assert: the app's endpoint ran, and the framework's error handling turned its NotFoundException into a problem.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Boiler not found.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Build_WithItsOwnFrameworkHooks_RegistersNoMagicCSharpDefaults()
    {
        // Act
        await using var app = MagicApp.Build<OwnHostingStartup>([]);

        // Assert: the app's own services are there; MagicCSharp's are not.
        Assert.NotNull(app.Services.GetService<BoilerRegistry>());
        Assert.Null(app.Services.GetService<IRequestIdHandler>());
    }

    [Fact]
    public void Build_FailsNamingAServiceThatCannotBeResolved()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => MagicApp.Build<MissingDependencyStartup>([]));

        // Assert
        Assert.Contains(typeof(BoilerInspectionScheduler).FullName!, exception.Message);
    }

    [Fact]
    public void Run_ReportsTheInnerExceptionOfAnAggregateAsFatal_AndRethrows()
    {
        // Act
        var exception = Assert.Throws<AggregateException>(() => MagicApp.Run<AggregateFailureStartup>([]));

        // Assert
        var fatal = Assert.IsType<InvalidOperationException>(Assert.Single(AggregateFailureStartup.Fatals));
        Assert.Same(exception.InnerException, fatal);
        Assert.Equal("The boiler registry is not configured.", fatal.Message);
    }

    [Fact]
    public void Run_DoesNotReportAnAbortedHostAsFatal()
    {
        // Act
        Assert.Throws<HostAbortedException>(() => MagicApp.Run<AbortedHostStartup>([]));

        // Assert
        Assert.Empty(AbortedHostStartup.Fatals);
    }

    [Fact]
    public void Run_CallsOnStartedOnce_ThenServesUntilStopped()
    {
        // Act: the host stops itself as soon as it has started.
        MagicApp.Run<StopsWhenStartedStartup>([]);

        // Assert
        Assert.Equal(["OnStarted", "ApplicationStarted"], StopsWhenStartedStartup.Events);
    }

    public class BoilerRegistry
    {
    }

    public interface IBoilerInspectionCalendar
    {
    }

    public class BoilerInspectionScheduler(IBoilerInspectionCalendar boilerInspectionCalendar)
    {
        public IBoilerInspectionCalendar Calendar { get; } = boilerInspectionCalendar;
    }

    /// <summary>MagicCSharp's defaults on a test server, without scheduling's lock files or log output.</summary>
    public abstract class TestServerStartup : MagicAppStartup
    {
        protected TestServerStartup()
        {
            Options = new MagicAppOptions { ShouldRegisterScheduling = false };
        }

        protected override void AddFrameworkServices(WebApplicationBuilder builder)
        {
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            base.AddFrameworkServices(builder);
        }
    }

    public class BoilersStartup : TestServerStartup
    {
        protected override void AddAppServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<BoilerRegistry>();
        }

        protected override void UseAppPipeline(WebApplication app)
        {
            app.MapGet("/boilers/{boilerId}", (long boilerId) =>
            {
                throw new NotFoundException("Boiler");
            });
        }
    }

    public class OwnHostingStartup : MagicAppStartup
    {
        protected override void AddFrameworkServices(WebApplicationBuilder builder)
        {
            builder.WebHost.UseTestServer();
        }

        protected override void AddAppServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<BoilerRegistry>();
        }

        protected override void UseFrameworkPipeline(WebApplication app, WebApplicationBuilder builder)
        {
        }
    }

    public class MissingDependencyStartup : TestServerStartup
    {
        protected override void AddAppServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<BoilerInspectionScheduler>();
        }
    }

    public class AggregateFailureStartup : TestServerStartup
    {
        public static List<Exception> Fatals { get; } = [];

        protected override void AddAppServices(IServiceCollection services, IConfiguration configuration)
        {
            throw new AggregateException(new InvalidOperationException("The boiler registry is not configured."));
        }

        protected override void OnFatal(Exception exception)
        {
            Fatals.Add(exception);
        }
    }

    public class AbortedHostStartup : TestServerStartup
    {
        public static List<Exception> Fatals { get; } = [];

        protected override void AddAppServices(IServiceCollection services, IConfiguration configuration)
        {
            throw new HostAbortedException();
        }

        protected override void OnFatal(Exception exception)
        {
            Fatals.Add(exception);
        }
    }

    public class StopsWhenStartedStartup : TestServerStartup
    {
        public static List<string> Events { get; } = [];

        protected override void AddAppServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddHostedService<StopWhenStartedService>();
        }

        protected override void OnStarted(WebApplication app, TimeSpan startupTime)
        {
            Events.Add("OnStarted");
        }
    }

    public class StopWhenStartedService(IHostApplicationLifetime hostApplicationLifetime) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            hostApplicationLifetime.ApplicationStarted.Register(RecordStartAndStop);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void RecordStartAndStop()
        {
            StopsWhenStartedStartup.Events.Add("ApplicationStarted");
            hostApplicationLifetime.StopApplication();
        }
    }
}
