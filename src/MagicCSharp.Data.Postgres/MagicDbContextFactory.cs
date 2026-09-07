using MagicCSharp.Data.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace MagicCSharp.Data.Postgres;

/// <summary>
///     Lets <c>dotnet ef migrations add</c> construct the context outside the running application.
///     <para>
///         The EF tools build the context without the application's dependency injection, so they cannot use the
///         registration in <c>Program.cs</c>. Derive from this and pass the database name; the tools find it by
///         type. Without one, <c>dotnet ef</c> falls back to booting the whole host just to read a connection
///         string.
///     </para>
///     <para>
///         Settings come from environment variables, falling back to the defaults given here, so the same command
///         works against a local container and against a named stack.
///     </para>
/// </summary>
/// <example>
///     <code language="csharp">
///     public class MagicShopContextFactory() : MagicDbContextFactory&lt;MagicShopContext&gt;("shop");
///     </code>
/// </example>
/// <param name="defaultDatabase">Database name when <c>{prefix}_NAME</c> is not set.</param>
/// <param name="defaultHost">Host when <c>{prefix}_HOST</c> is not set. Defaults to localhost.</param>
/// <param name="defaultPort">Port when <c>{prefix}_PORT</c> is not set. Defaults to 5432.</param>
/// <param name="defaultUser">User when <c>{prefix}_USER</c> is not set. Defaults to postgres.</param>
/// <param name="defaultPassword">Password when <c>{prefix}_PASSWORD</c> is not set.</param>
/// <param name="configureNpgsql">
///     Hook for a provider plugin the model needs at design time, such as pgvector's <c>UseVector()</c> — without
///     it, generating a migration for a context with a vector column fails.
/// </param>
/// <param name="configPrefix">Environment variable prefix. Defaults to <c>DB</c>.</param>
public abstract class MagicDbContextFactory<TContext>(
    string defaultDatabase,
    string defaultHost = "localhost",
    string defaultPort = "5432",
    string defaultUser = "postgres",
    string defaultPassword = "postgres",
    Action<NpgsqlDbContextOptionsBuilder>? configureNpgsql = null,
    string configPrefix = "DB") : IDesignTimeDbContextFactory<TContext>
    where TContext : MagicDbContext
{
    public TContext CreateDbContext(string[] args)
    {
        var host = Read("HOST", defaultHost);
        var port = Read("PORT", defaultPort);
        var database = Read("NAME", defaultDatabase);
        var user = Read("USER", defaultUser);
        var password = Read("PASSWORD", defaultPassword);

        var connectionString = $"Host={host};Port={port};Username={user};Password={password};Database={database}";

        Console.WriteLine($"Using database {user}@{host}:{port}/{database}");

        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseNpgsql(connectionString, npgsql => configureNpgsql?.Invoke(npgsql));

        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
    }

    private string Read(string suffix, string fallback)
    {
        var value = Environment.GetEnvironmentVariable($"{configPrefix}_{suffix}");
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
