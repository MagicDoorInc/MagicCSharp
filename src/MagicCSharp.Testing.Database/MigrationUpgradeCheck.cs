using DotNet.Testcontainers.Configurations;
using MagicCSharp.Data.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MagicCSharp.Testing.Database;

/// <summary>
///     Checks that a context's migrations take a database to the latest migration, and that running them again
///     changes nothing.
///     <para>
///         The database starts empty, or from the context's baseline dump when one is there, so the check upgrades
///         what production runs rather than only building from nothing. Each run gets a fresh database in the given
///         container, dropped afterwards. It names no test framework: a failure throws
///         <see cref="MigrationUpgradeException" />, which fails a test in any of them.
///     </para>
/// </summary>
public static class MigrationUpgradeCheck
{
    /// <summary>Upgrades a fresh database of <typeparamref name="TContext" /> and checks the result.</summary>
    /// <typeparam name="TContext">A context with <c>[MigrationsDatabase]</c>, constructible from its options.</typeparam>
    /// <exception cref="MigrationUpgradeException">The context has no migrations, or the upgrade did not reach the latest one.</exception>
    public static async Task Verify<TContext>(PostgreSqlContainer container, MigrationUpgradeOptions options, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var databaseName = MigrationsDatabaseAttribute.NameOf(typeof(TContext));
        var upgradedDatabaseName = $"{databaseName}_upgrade_{Guid.NewGuid():N}".Substring(0, Math.Min(63, databaseName.Length + 21));

        await RunOnServer(container, $"CREATE DATABASE \"{upgradedDatabaseName}\";", cancellationToken);
        try
        {
            var upgradedDatabase = new UpgradedDatabase
            {
                Container = container,
                DatabaseName = databaseName,
                UpgradedDatabaseName = upgradedDatabaseName,
                Options = options,
            };
            await RestoreBaselineIfPresent(upgradedDatabase, cancellationToken);
            await UpgradeAndCompare<TContext>(upgradedDatabase, cancellationToken);
        }
        finally
        {
            await RunOnServer(container, $"DROP DATABASE IF EXISTS \"{upgradedDatabaseName}\" WITH (FORCE);", CancellationToken.None);
        }
    }

    private static async Task UpgradeAndCompare<TContext>(UpgradedDatabase upgradedDatabase, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var databaseName = upgradedDatabase.DatabaseName;
        var options = upgradedDatabase.Options;
        var connectionString = new NpgsqlConnectionStringBuilder(upgradedDatabase.Container.GetConnectionString())
        {
            Database = upgradedDatabase.UpgradedDatabaseName,
            Pooling = false,
        }.ConnectionString;
        var contextOptions = new DbContextOptionsBuilder<TContext>().UseNpgsql(connectionString, options.ConfigureNpgsql).Options;
        await using var context = (TContext)Activator.CreateInstance(typeof(TContext), contextOptions)!;

        var codeMigrations = context.Database.GetMigrations().ToArray();
        if (codeMigrations.Length == 0)
        {
            throw new MigrationUpgradeException($"{typeof(TContext).Name} has no migrations to check.");
        }

        var migrationsBeforeUpgrade = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        options.WriteLine?.Invoke($"{databaseName}: {migrationsBeforeUpgrade.Length} applied migrations before upgrade; {codeMigrations.Length} in code.");

        await context.Database.MigrateAsync(cancellationToken);
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        if (!appliedMigrations.SequenceEqual(codeMigrations))
        {
            throw new MigrationUpgradeException($"{databaseName}: after the upgrade the database has [{string.Join(", ", appliedMigrations)}], " +
                                                $"the code has [{string.Join(", ", codeMigrations)}].");
        }

        await context.Database.MigrateAsync(cancellationToken);
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        var migrationsAfterSecondRun = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        if (pendingMigrations.Length > 0 || !migrationsAfterSecondRun.SequenceEqual(appliedMigrations))
        {
            throw new MigrationUpgradeException($"{databaseName}: running the migrations a second time changed the database.");
        }
    }

    private static async Task RestoreBaselineIfPresent(UpgradedDatabase upgradedDatabase, CancellationToken cancellationToken)
    {
        var container = upgradedDatabase.Container;
        var databaseName = upgradedDatabase.DatabaseName;
        var upgradedDatabaseName = upgradedDatabase.UpgradedDatabaseName;
        var options = upgradedDatabase.Options;
        var baselinePath = Path.Combine(options.BaselineDirectory, $"{databaseName}_baseline.dump");
        if (!File.Exists(baselinePath))
        {
            options.WriteLine?.Invoke($"{databaseName}: baseline absent; testing migrations from an empty database.");
            return;
        }

        var archive = await File.ReadAllBytesAsync(baselinePath, cancellationToken);
        options.WriteLine?.Invoke($"{databaseName}: restoring {Path.GetFileName(baselinePath)} ({archive.Length} bytes).");

        var archivePathInContainer = $"/tmp/{upgradedDatabaseName}.dump";
        try
        {
            await container.CopyAsync(archive, archivePathInContainer, fileMode: UnixFileModes.UserRead | UnixFileModes.UserWrite, ct: cancellationToken);
            var userName = new NpgsqlConnectionStringBuilder(container.GetConnectionString()).Username;
            var restoreResult = await container.ExecAsync([
                "pg_restore",
                "--username", userName!,
                "--dbname", upgradedDatabaseName,
                "--no-owner",
                "--no-acl",
                "--no-password",
                "--exit-on-error",
                archivePathInContainer,
            ], cancellationToken);
            if (restoreResult.ExitCode != 0)
            {
                // pg_restore's own output can quote rows of the baseline, so it stays out of the message.
                throw new MigrationUpgradeException($"pg_restore failed for {Path.GetFileName(baselinePath)} (exit code {restoreResult.ExitCode}).");
            }
        }
        finally
        {
            await container.ExecAsync(["rm", "-f", "--", archivePathInContainer], CancellationToken.None);
        }
    }

    private static async Task RunOnServer(PostgreSqlContainer container, string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Database names are identifiers, which cannot be parameters; they come from an attribute and a GUID, not input.
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>The database a check upgrades: the context's database name and the throwaway copy it runs on.</summary>
    private sealed record UpgradedDatabase
    {
        public required PostgreSqlContainer Container { get; init; }

        public required string DatabaseName { get; init; }

        public required string UpgradedDatabaseName { get; init; }

        public required MigrationUpgradeOptions Options { get; init; }
    }
}
