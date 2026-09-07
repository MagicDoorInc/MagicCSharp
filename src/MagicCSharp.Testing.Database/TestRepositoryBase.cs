using System.Collections.Concurrent;
using System.Data;
using MagicCSharp.Data.EntityFramework;
using MagicCSharp.Data.Postgres;
using MagicCSharp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace MagicCSharp.Testing.Database;

/// <summary>
///     Base class for repository tests that run against a real PostgreSQL.
///     <para>
///         A repository is mostly translation — a filter into SQL, a row into an entity — and the in-memory and
///         SQLite providers translate differently from Postgres. A test that passes against them proves the C#
///         and not the query, which is the half that breaks. So this runs against the real thing.
///     </para>
///     <para>
///         Cost is managed by sharing: one container for the whole suite, one logical database per test class
///         (so classes can run in parallel without seeing each other's rows), schema created once per database,
///         and a truncate between tests rather than a recreate.
///     </para>
///     <para>
///         Requires Docker. Mark tests deriving from this with a trait so a machine without Docker can filter
///         them out.
///     </para>
/// </summary>
/// <typeparam name="TContext">The context under test.</typeparam>
public abstract class TestRepositoryBase<TContext> : IAsyncLifetime
    where TContext : DbContext
{
    private static readonly ConcurrentDictionary<string, bool> InitializedDatabases = new ConcurrentDictionary<string, bool>();
    private static readonly SemaphoreSlim InitLock = new SemaphoreSlim(1, 1);
    private static readonly ConcurrentDictionary<string, DbContextOptions<TContext>> OptionsByDatabase =
        new ConcurrentDictionary<string, DbContextOptions<TContext>>();
    private static readonly SemaphoreSlim StartLock = new SemaphoreSlim(1, 1);

    private static PostgreSqlContainer? container;
    private static volatile bool containerStarted;

    protected TestRepositoryBase()
    {
        Clock = new FakeClock();
        KeyGen = new FakeKeyGen(Clock);
    }

    /// <summary>The clock the repository under test reads. Move it to test time-dependent behaviour.</summary>
    protected FakeClock Clock { get; }

    /// <summary>Deterministic ids, derived from <see cref="Clock" />.</summary>
    protected FakeKeyGen KeyGen { get; }

    /// <summary>The factory to hand the repository under test.</summary>
    protected IDbContextFactory<TContext> DbContextFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await EnsureContainerStarted();

        var databaseName = GetDatabaseName();

        var options = OptionsByDatabase.GetOrAdd(databaseName, BuildOptions);

        await EnsureDatabaseInitialized(databaseName, options);

        DbContextFactory = new OptionsDbContextFactory(options, CreateDbContext);
    }

    public async Task DisposeAsync()
    {
        await CleanDatabase();
    }

    /// <summary>
    ///     The container image. Override to match what production runs, or to add an extension — a context with
    ///     a <c>vector</c> column needs <c>pgvector/pgvector:pg17</c>.
    /// </summary>
    protected virtual string ContainerImage => "postgres:17";

    /// <summary>
    ///     The logical database for this test class. Defaults to the context and class name, which keeps
    ///     classes isolated. Override to return the same name from several classes that should share a schema.
    /// </summary>
    protected virtual string GetDatabaseName()
    {
        return $"{typeof(TContext).Name}_{GetType().Name}".ToLowerInvariant();
    }

    /// <summary>
    ///     How the schema is created. <c>EnsureCreatedAsync</c> builds it straight from the model, which is what
    ///     you want when testing repositories in a library with no migrations. Override to
    ///     <c>Database.MigrateAsync()</c> to test that the migrations themselves produce a working schema.
    /// </summary>
    protected virtual Task InitializeDatabase(TContext context)
    {
        return context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    ///     Register a Postgres type plugin on the data source, e.g. pgvector's <c>UseVector()</c>.
    /// </summary>
    protected virtual void ConfigureDataSource(NpgsqlDataSourceBuilder dataSourceBuilder)
    {
    }

    /// <summary>
    ///     Configure provider options inside <c>UseNpgsql</c>.
    /// </summary>
    protected virtual void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsqlOptions)
    {
    }

    /// <summary>
    ///     Construct the context. The default calls a constructor taking <c>DbContextOptions&lt;TContext&gt;</c>;
    ///     override when the context needs more.
    /// </summary>
    protected virtual TContext CreateDbContext(DbContextOptions<TContext> options)
    {
        return Activator.CreateInstance(typeof(TContext), options) as TContext ??
               throw new InvalidOperationException($"Could not construct {typeof(TContext).Name} from DbContextOptions. Override CreateDbContext.");
    }

    /// <summary>
    ///     Empty every table, keeping the schema. Runs after each test; call it mid-test to reset between
    ///     phases.
    /// </summary>
    protected async Task CleanDatabase()
    {
        await using var context = DbContextFactory.CreateDbContext();
        var connection = context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              DO $$
                              DECLARE
                                  r RECORD;
                              BEGIN
                                  FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
                                      EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' CASCADE';
                                  END LOOP;
                              END $$;
                              """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureContainerStarted()
    {
        if (containerStarted)
        {
            return;
        }

        await StartLock.WaitAsync();
        try
        {
            if (containerStarted)
            {
                return;
            }

            container = new PostgreSqlBuilder().WithImage(ContainerImage).WithCommand("-c", "max_connections=500").Build();
            await container.StartAsync();
            containerStarted = true;
        }
        finally
        {
            StartLock.Release();
        }
    }

    private DbContextOptions<TContext> BuildOptions(string databaseName)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(container!.GetConnectionString())
        {
            Database = databaseName,
            IncludeErrorDetail = true,
        }.ConnectionString;

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
            .EnableDynamicJson()
            .ConfigureJsonOptions(JsonDefaults.Options);
        ConfigureDataSource(dataSourceBuilder);

        var builder = new DbContextOptionsBuilder<TContext>().UseNpgsql(dataSourceBuilder.Build(), npgsql =>
        {
            npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            ConfigureNpgsql(npgsql);
        });

        // Each test class builds its own provider, which EF warns about; that is exactly the intent here.
        builder.ConfigureWarnings(warnings => warnings
            .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
            .Ignore(CoreEventId.SensitiveDataLoggingEnabledWarning)
            .Throw(RelationalEventId.MultipleCollectionIncludeWarning));

        if (typeof(MagicDbContext).IsAssignableFrom(typeof(TContext)))
        {
            builder.AddInterceptors(UtcDateTimeOffsetCommandInterceptor.Instance);
        }

        return builder.Options;
    }

    private async Task EnsureDatabaseInitialized(string databaseName, DbContextOptions<TContext> options)
    {
        if (InitializedDatabases.ContainsKey(databaseName))
        {
            return;
        }

        await InitLock.WaitAsync();
        try
        {
            if (InitializedDatabases.ContainsKey(databaseName))
            {
                return;
            }

            await CreateLogicalDatabase(databaseName);

            await using var context = CreateDbContext(options);
            await InitializeDatabase(context);

            InitializedDatabases[databaseName] = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static async Task CreateLogicalDatabase(string databaseName)
    {
        await using var connection = new NpgsqlConnection(container!.GetConnectionString());
        await connection.OpenAsync();

        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name;";
        exists.Parameters.AddWithValue("name", databaseName);

        if (await exists.ExecuteScalarAsync() != null)
        {
            return;
        }

        await using var create = connection.CreateCommand();
        // Quoted, not parameterized: CREATE DATABASE takes an identifier, which cannot be a parameter. The
        // name comes from a type name, not from input.
        create.CommandText = $"CREATE DATABASE \"{databaseName}\";";
        await create.ExecuteNonQueryAsync();
    }

    private sealed class OptionsDbContextFactory(
        DbContextOptions<TContext> options,
        Func<DbContextOptions<TContext>, TContext> construct) : IDbContextFactory<TContext>
    {
        public TContext CreateDbContext()
        {
            return construct(options);
        }
    }
}
