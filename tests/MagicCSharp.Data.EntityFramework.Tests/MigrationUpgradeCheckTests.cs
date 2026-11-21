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

    private readonly List<string> progressLines = [];

    public void Dispose()
    {
        Directory.Delete(baselineDirectory, true);
    }

    [Fact]
    public async Task MigrationUpgradeCheck_UpgradesFromARestoredBaseline()
    {
        // Arrange: production is one migration behind.
        await WriteBaseline("boilers_one_behind", "");

        // Act
        await MigrationUpgradeCheck.Verify<BoilersDbContext>(postgres.Container, Options(), CancellationToken.None);

        // Assert
        Assert.Contains(progressLines, line => line.StartsWith("boilers: restoring boilers_baseline.dump", StringComparison.Ordinal));
        Assert.Contains("boilers: 1 applied migrations before upgrade; 2 in code.", progressLines);
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
        return new MigrationUpgradeOptions { BaselineDirectory = baselineDirectory, WriteLine = progressLines.Add };
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
