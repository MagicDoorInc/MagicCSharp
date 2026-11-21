using MagicCSharp.Data.EntityFramework.Tests.Fixtures;
using MagicCSharp.Infrastructure.KeyGen;
using MagicCSharp.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>
///     <c>AddRepositoriesFrom&lt;TContext&gt;()</c> over this test assembly, whose repositories are the fixtures. No
///     database: registration and construction only.
/// </summary>
public class RepositoryRegistrationModuleTests
{
    [Fact]
    public void AddRepositoriesFrom_RegistersEachRepositoryUnderItsOwnInterface_Scoped_AndResolvesIt()
    {
        // Arrange
        var services = ServicesWithRepositoryDependencies();

        // Act
        services.AddRepositoriesFrom<TestDbContext>();

        // Assert
        var notesRegistration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(INotesRepository));
        Assert.Equal(typeof(NotesRepository), notesRegistration.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, notesRegistration.Lifetime);
        var widgetsRegistration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IWidgetsRepository));
        Assert.Equal(typeof(WidgetsRepository), widgetsRegistration.ImplementationType);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<NotesRepository>(scope.ServiceProvider.GetRequiredService<INotesRepository>());
    }

    [Fact]
    public void AddRepositoriesFrom_KeepsAHandWrittenRegistration()
    {
        // Arrange
        var services = ServicesWithRepositoryDependencies();
        services.AddSingleton<IWidgetsRepository, WidgetsRepository>();

        // Act
        services.AddRepositoriesFrom<TestDbContext>();

        // Assert
        var widgetsRegistration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IWidgetsRepository));
        Assert.Equal(ServiceLifetime.Singleton, widgetsRegistration.Lifetime);
    }

    private static ServiceCollection ServicesWithRepositoryDependencies()
    {
        var services = new ServiceCollection();
        var fakeTimeProvider = new FakeTimeProvider();
        services.AddSingleton<TimeProvider>(fakeTimeProvider);
        services.AddSingleton<IKeyGenService>(new FakeKeyGen(fakeTimeProvider));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IDbContextFactory<TestDbContext>, UnopenedContextFactory>();
        return services;
    }

    /// <summary>Constructing a repository opens no context, so these tests need a factory that is never asked for one.</summary>
    private class UnopenedContextFactory : IDbContextFactory<TestDbContext>
    {
        public TestDbContext CreateDbContext()
        {
            throw new NotSupportedException("Registration tests open no database.");
        }
    }
}
