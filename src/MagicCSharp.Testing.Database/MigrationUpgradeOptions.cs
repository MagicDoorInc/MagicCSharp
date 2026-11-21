using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace MagicCSharp.Testing.Database;

/// <summary>How <see cref="MigrationUpgradeCheck" /> builds the database it upgrades, and where it reports progress.</summary>
public sealed record MigrationUpgradeOptions
{
    /// <summary>
    ///     Where <c>{database}_baseline.dump</c> is looked for: a <c>pg_dump -Fc</c> archive of the database as
    ///     production has it, which the check upgrades from. Defaults to the test output's <c>Migrations</c> folder.
    /// </summary>
    public string BaselineDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "Migrations");

    /// <summary>Provider options the model needs, such as pgvector's <c>UseVector()</c>.</summary>
    public Action<NpgsqlDbContextOptionsBuilder>? ConfigureNpgsql { get; init; }

    /// <summary>Receives one line per stage, e.g. a test's output helper.</summary>
    public Action<string>? WriteLine { get; init; }
}
