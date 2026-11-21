using MagicCSharp.Data.EntityFramework.Tests.Fixtures.Boilers;
using MagicCSharp.Testing.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>Upgrading from a baseline dump, as production's database is upgraded.</summary>
[Trait("Category", "Database")]
public class MigrationUpgradeCheckTests(PostgresContainerFixture postgres) : IClassFixture<PostgresContainerFixture>, IDisposable
{
    private const string FirstMigration = "20261121165358_CreateBoilers";

    private readonly string baselineDirectory = Directory.CreateTempSubdirectory("boilers-baseline-").FullName;

    public void Dispose()
    {
        Directory.Delete(baselineDirectory, true);
    }

    [Fact]
    public async Task MigrationUpgradeCheck_UpgradesARestoredBaselineWithRows_AndDropsTheCopyItUpgraded()
    {
        // Arrange: production is one migration behind and holds a boiler.
        await WriteBaseline("boilers_one_behind", """INSERT INTO "boilers" ("tenant_id") VALUES (1);""");

        // Act
        await MigrationUpgradeCheck.Verify<BoilersDbContext>(postgres.Container, Options(), CancellationToken.None);

        // Assert: the check finished, and no copy it upgraded is left on the server.
        Assert.Equal(0, await CountDatabasesNamedLike("boilers\\_upgrade\\_%"));
    }

    [Fact]
    public async Task MigrationUpgradeCheck_UpgradesTheBaselinesSchema_NotAnEmptyDatabase()
    {
        // Arrange: production already has the column the second migration adds, so upgrading its schema must fail.
        await WriteBaseline("boilers_with_column", """ALTER TABLE "boilers" ADD COLUMN "serial_number" text;""");

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            MigrationUpgradeCheck.Verify<BoilersDbContext>(postgres.Container, Options(), CancellationToken.None));

        // Assert
        Assert.Equal(PostgresErrorCodes.DuplicateColumn, exception.SqlState);
    }

    [Fact]
    public async Task MigrationUpgradeCheck_FailsWhenTheBaselineHoldsAMigrationTheCodeLacks()
    {
        // Arrange: production ran a migration that was later deleted from the code.
        await WriteBaseline("boilers_with_removed",
            """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20250101000000_RemovedMigration', '9.0.0');""");

        // Act
        var exception = await Assert.ThrowsAsync<MigrationUpgradeException>(() =>
            MigrationUpgradeCheck.Verify<BoilersDbContext>(postgres.Container, Options(), CancellationToken.None));

        // Assert
        Assert.Contains("20250101000000_RemovedMigration", exception.Message);
    }

    private MigrationUpgradeOptions Options()
    {
        return new MigrationUpgradeOptions { BaselineDirectory = baselineDirectory };
    }

    /// <summary>A database at the first migration, plus <paramref name="extraSql" />, dumped as the baseline.</summary>
    private async Task WriteBaseline(string sourceDatabaseName, string extraSql)
    {
        await RunOnServer($"CREATE DATABASE \"{sourceDatabaseName}\";", null);
        var connectionString = new NpgsqlConnectionStringBuilder(postgres.Container.GetConnectionString()) { Database = sourceDatabaseName, Pooling = false }.ConnectionString;
        await using (var boilersDbContext = new BoilersDbContext(new DbContextOptionsBuilder<BoilersDbContext>().UseNpgsql(connectionString).Options))
        {
            await boilersDbContext.GetService<IMigrator>().MigrateAsync(FirstMigration);
        }

        if (extraSql.Length > 0)
        {
            await RunOnServer(extraSql, sourceDatabaseName);
        }

        var userName = new NpgsqlConnectionStringBuilder(postgres.Container.GetConnectionString()).Username!;
        var dumpResult = await postgres.Container.ExecAsync(["pg_dump", "--username", userName, "--format", "custom", "--file", "/tmp/baseline.dump", sourceDatabaseName]);
        Assert.Equal(0, dumpResult.ExitCode);
        var archive = await postgres.Container.ReadFileAsync("/tmp/baseline.dump");
        await File.WriteAllBytesAsync(Path.Combine(baselineDirectory, "boilers_baseline.dump"), archive);
    }

    private async Task<long> CountDatabasesNamedLike(string pattern)
    {
        await using var connection = new NpgsqlConnection(postgres.Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM pg_database WHERE datname LIKE @pattern";
        command.Parameters.AddWithValue("pattern", pattern);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task RunOnServer(string commandText, string? databaseName)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(postgres.Container.GetConnectionString());
        if (databaseName != null)
        {
            connectionStringBuilder.Database = databaseName;
        }

        await using var connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
