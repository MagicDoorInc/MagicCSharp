using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;
using Xunit.Abstractions;

namespace MagicCSharp.Testing.Database;

/// <summary>
///     One test per context that its migrations upgrade a database to the latest one: derive from this with the
///     context, and the test is there.
///     <para>
///         The check itself is <see cref="MigrationUpgradeCheck" />; this runs it in a PostgreSQL container shared by
///         every migration test of the context. Requires Docker: mark the deriving class with a trait, as for
///         <see cref="TestRepositoryBase{TContext}" />.
///     </para>
/// </summary>
/// <example>
///     <code language="csharp">
///     [Trait("Category", "Database")]
///     public class ShopMigrationTests(ITestOutputHelper output) : MigrationTestBase&lt;MagicShopContext&gt;(output);
///     </code>
/// </example>
public abstract class MigrationTestBase<TContext>(ITestOutputHelper output)
    where TContext : DbContext
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<PostgreSqlContainer>>> ContainersByImage =
        new ConcurrentDictionary<string, Lazy<Task<PostgreSqlContainer>>>();

    /// <summary>The container image; a context with a <c>vector</c> column needs <c>pgvector/pgvector:pg17</c>.</summary>
    protected virtual string ContainerImage
    {
        get
        {
            return "postgres:17";
        }
    }

    [Fact]
    public async Task MigrationsUpgradeToLatest()
    {
        var container = await ContainersByImage.GetOrAdd(ContainerImage, StartContainer).Value;

        await MigrationUpgradeCheck.Verify<TContext>(container, new MigrationUpgradeOptions
        {
            ConfigureNpgsql = ConfigureNpgsql,
            WriteLine = output.WriteLine,
        }, CancellationToken.None);
    }

    /// <summary>Provider options the model needs, such as pgvector's <c>UseVector()</c>.</summary>
    protected virtual void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsqlOptions)
    {
    }

    private static Lazy<Task<PostgreSqlContainer>> StartContainer(string image)
    {
        return new Lazy<Task<PostgreSqlContainer>>(async () =>
        {
            var postgreSqlContainer = new PostgreSqlBuilder(image).Build();
            await postgreSqlContainer.StartAsync();
            return postgreSqlContainer;
        });
    }
}
