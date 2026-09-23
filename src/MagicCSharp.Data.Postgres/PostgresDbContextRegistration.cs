using MagicCSharp.Data.EntityFramework;
using MagicCSharp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MagicCSharp.Data.Postgres;

/// <summary>
///     Registers a pooled <see cref="IDbContextFactory{TContext}" /> over PostgreSQL, configured the way the
///     MagicCSharp repositories expect.
/// </summary>
public static class PostgresDbContextRegistration
{
    /// <summary>
    ///     Read connection settings from configuration and register a pooled context factory for
    ///     <typeparamref name="TContext" />.
    ///     <para>
    ///         The repositories open a context per operation, which is only cheap because the factory is pooled —
    ///         pooling reuses the context instance and its internal service provider rather than rebuilding the
    ///         model each time.
    ///     </para>
    ///     <para>
    ///         The connection is opened once during registration, so a wrong host or password fails at startup
    ///         with a clear error instead of on the first request that happens to need the database. Set
    ///         <c>{prefix}_VERIFY_CONNECTION</c> to <c>false</c> where that is wrong — a test that swaps the
    ///         repositories out, or a container that starts before its database.
    ///     </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    ///     Configuration to read <c>{prefix}_HOST</c>, <c>_PORT</c>, <c>_NAME</c>, <c>_USER</c> and
    ///     <c>_PASSWORD</c> from. Each is required; a missing one throws rather than silently defaulting.
    /// </param>
    /// <param name="options">Connection pool and timeout settings. The defaults suit a typical service.</param>
    /// <param name="configureDataSource">
    ///     Hook to configure the Npgsql data source before it is built — where a type plugin such as pgvector's
    ///     <c>UseVector()</c> goes.
    /// </param>
    /// <param name="configureNpgsql">Hook to configure the provider options inside <c>UseNpgsql</c>.</param>
    /// <param name="configPrefix">
    ///     Prefix for the configuration keys. Defaults to <c>DB</c>. Pass another, e.g. <c>VECTOR_DB</c>, to point
    ///     a second context at a different database.
    /// </param>
    public static IServiceCollection AddPostgresDbContextFactory<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        PostgresConnectionOptions? options = null,
        Action<NpgsqlDataSourceBuilder>? configureDataSource = null,
        Action<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder>? configureNpgsql = null,
        string configPrefix = "DB")
        where TContext : MagicDbContext
    {
        options ??= new PostgresConnectionOptions();

        var host = Required(configuration, $"{configPrefix}_HOST");
        var port = Required(configuration, $"{configPrefix}_PORT");
        var database = Required(configuration, $"{configPrefix}_NAME");
        var user = Required(configuration, $"{configPrefix}_USER");
        var password = Required(configuration, $"{configPrefix}_PASSWORD");

        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(port),
            Database = database,
            Username = user,
            Password = password,
            Timeout = options.ConnectTimeoutSeconds,
            CommandTimeout = options.CommandTimeoutSeconds,
            MaxPoolSize = options.MaxPoolSize,
            MinPoolSize = options.MinPoolSize,
            ConnectionIdleLifetime = options.ConnectionIdleLifetimeSeconds,
            KeepAlive = options.KeepAliveSeconds,
            TcpKeepAlive = true,
            IncludeErrorDetail = options.IncludeErrorDetail,
        }.ConnectionString;

        // Configuration overrides the code default, so a test that replaces every repository, or a
        // container that starts before its database, can turn the check off without editing the
        // registration it is otherwise happy with.
        if (Verify(configuration, $"{configPrefix}_VERIFY_CONNECTION", options.VerifyConnectionOnStartup))
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
        }

        services.AddPooledDbContextFactory<TContext>(builder =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
                .EnableDynamicJson()
                .ConfigureJsonOptions(JsonDefaults.Options);
            configureDataSource?.Invoke(dataSourceBuilder);

            builder.UseNpgsql(dataSourceBuilder.Build(), npgsql =>
            {
                npgsql.EnableRetryOnFailure(options.RetryCount, TimeSpan.FromSeconds(options.RetryMaxDelaySeconds), null);
                configureNpgsql?.Invoke(npgsql);
            });

            // A query that Includes two collections at once multiplies the rows and is almost never what was
            // meant. Throwing turns a silent performance cliff into a compile-once, fix-once error.
            builder.ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.MultipleCollectionIncludeWarning));

            builder.AddInterceptors(UtcDateTimeOffsetCommandInterceptor.Instance);
        });

        return services;
    }

    /// <summary>
    ///     Whether to open a connection now. The configured value wins when it is set and parses; anything
    ///     else falls back to what the caller asked for.
    /// </summary>
    private static bool Verify(IConfiguration configuration, string key, bool fallback)
    {
        var value = configuration[key];

        return bool.TryParse(value, out var configured) ? configured : fallback;
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{key}' is missing or empty.");
        }

        return value;
    }
}

/// <summary>
///     Connection pool and timeout settings for <see cref="PostgresDbContextRegistration.AddPostgresDbContextFactory{TContext}" />.
/// </summary>
public record PostgresConnectionOptions
{
    /// <summary>Seconds to wait for a connection before giving up.</summary>
    public int ConnectTimeoutSeconds { get; init; } = 60;

    /// <summary>Seconds a single command may run before it is cancelled.</summary>
    public int CommandTimeoutSeconds { get; init; } = 120;

    /// <summary>
    ///     Upper bound on pooled connections for this process. Size it against the server's limit divided by the
    ///     number of instances you run, or a rolling deploy will exhaust the server.
    /// </summary>
    public int MaxPoolSize { get; init; } = 200;

    /// <summary>Connections kept open while idle, so a burst does not pay connection setup.</summary>
    public int MinPoolSize { get; init; } = 20;

    /// <summary>Seconds an idle connection above the minimum is kept before being closed.</summary>
    public int ConnectionIdleLifetimeSeconds { get; init; } = 600;

    /// <summary>Seconds between keepalives, so a connection dropped by a firewall is noticed.</summary>
    public int KeepAliveSeconds { get; init; } = 60;

    /// <summary>
    ///     Include the offending value in constraint-violation errors. On by default because it turns
    ///     "duplicate key" into "duplicate key (email)=(x@y.com)"; turn it off if those errors reach somewhere
    ///     that must not see row data.
    /// </summary>
    public bool IncludeErrorDetail { get; init; } = true;

    /// <summary>Times a transient failure is retried before it surfaces.</summary>
    public int RetryCount { get; init; } = 5;

    /// <summary>Longest delay between retries.</summary>
    public int RetryMaxDelaySeconds { get; init; } = 30;

    /// <summary>
    ///     Open a connection during registration so misconfiguration fails at startup. Turn off where the
    ///     database legitimately starts after the application.
    ///     <para>
    ///         Configuration wins over this: <c>{prefix}_VERIFY_CONNECTION=false</c> turns the check off
    ///         without a code change.
    ///     </para>
    /// </summary>
    public bool VerifyConnectionOnStartup { get; init; } = true;
}
