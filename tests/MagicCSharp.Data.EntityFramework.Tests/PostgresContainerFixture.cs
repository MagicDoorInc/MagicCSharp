using Testcontainers.PostgreSql;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>One PostgreSQL container for a test class that makes its own databases in it.</summary>
public class PostgresContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync()
    {
        return Container.StartAsync();
    }

    public Task DisposeAsync()
    {
        return Container.DisposeAsync().AsTask();
    }
}
