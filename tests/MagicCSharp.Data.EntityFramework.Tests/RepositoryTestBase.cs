using MagicCSharp.Data.EntityFramework.Tests.Fixtures;
using MagicCSharp.Testing.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>
///     A real PostgreSQL in a container, and the repository under test wired to it.
///     <para>
///         These are the tests that a fake <c>DbContext</c> cannot replace. A repository's job is to turn a
///         filter into SQL and a row into an entity, and only the database can say whether it did. Every
///         one of these would pass against a stubbed context while the query was wrong.
///     </para>
/// </summary>
[Trait("Category", "Database")]
public abstract class RepositoryTestBase : TestRepositoryBase<TestDbContext>
{
    protected WidgetsRepository Widgets => new WidgetsRepository(DbContextFactory, Clock, NullLoggerFactory.Instance)
    {
        Ids = KeyGen,
    };

    protected ContactsRepository Contacts => new ContactsRepository(DbContextFactory, Clock, NullLoggerFactory.Instance)
    {
        Ids = KeyGen,
    };

    protected ContactsSoftDeleteRepository DeletableContacts =>
        new ContactsSoftDeleteRepository(DbContextFactory, Clock, NullLoggerFactory.Instance) { Ids = KeyGen };

    protected static ContactEdit AContact(string name = "Jane Smith", string city = "Springfield")
    {
        return new ContactEdit { Name = name, City = city };
    }

    /// <summary>
    ///     Reads one value with raw SQL, for asserting what is actually in a column rather than what the
    ///     repository hands back — the two differ exactly where the mapping is wrong.
    /// </summary>
    protected async Task<string?> ScalarText(string sql)
    {
        await using var context = DbContextFactory.CreateDbContext();
        var connection = context.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (await command.ExecuteScalarAsync())?.ToString();
    }

    protected static WidgetEdit AWidget(
        string name = "Widget",
        string? description = null,
        int quantity = 1,
        WidgetStatus status = WidgetStatus.Draft)
    {
        return new WidgetEdit
        {
            Name = name,
            Description = description,
            Quantity = quantity,
            Status = status,
        };
    }
}
