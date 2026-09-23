# MagicCSharp.Testing.Database

Repository integration testing against a real PostgreSQL, via Testcontainers. One container for the whole
suite, a database per test class, a fake clock and key generator already wired. Requires Docker.

Add it to the test project beside your data project — the one with the DALs and repositories. Split out from
`MagicCSharp.Testing` on purpose: the fakes there have no heavy dependencies, and a project that only wants
`FakeTimeProvider` should not pull in Testcontainers, xUnit and Npgsql.

```bash
dotnet add package MagicCSharp.Testing.Database
```

## Why a real database

A repository is mostly translation — a filter into SQL, a row into an entity. The in-memory and SQLite
providers translate differently from Postgres, so a test that passes against them proves the C# and not the
query, which is the half that breaks. Enum-as-string columns, `jsonb` round-trips and `timestamptz` offset
rules have no equivalent anywhere else.

MagicCSharp's own repository bases are tested this way — `tests/MagicCSharp.Data.EntityFramework.Tests` is
fifty tests over a real Postgres, covering create, filter, update, delete, pagination, soft delete and the
search column. It is the worked example of everything below.

## Cost control

One container for the whole suite. One logical database per test class, so classes run in parallel without
seeing each other's rows. Schema created once per database. Tables truncated between tests rather than
recreated.

## Use

```csharp
[Trait("Category", "Integration")]
public class OrdersRepositoryTests : TestRepositoryBase<MagicShopContext>
{
    private OrdersEfRepository OrdersRepository =>
        new OrdersEfRepository(DbContextFactory, KeyGen, TimeProvider, NullLoggerFactory.Instance);

    [Fact]
    public async Task Create_assigns_created_and_updated_from_the_clock()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

        var order = await OrdersRepository.Create(new OrderEdit
        {
            CustomerId = 7,
            Total = 42.50m,
            Status = OrderStatus.Pending,
        });

        Assert.Equal(TimeProvider.GetUtcNow(), order.Created);
    }
}
```

`TimeProvider` is a `FakeTimeProvider` starting at a fixed instant (1 January 2026, UTC), `KeyGen` a
`FakeKeyGen` derived from it, and `DbContextFactory` the pooled factory for your context — the same three
things the repository takes in production. The clock only moves forward: set it in the test's constructor,
then advance it.

Override `ContainerImage` to match production, or to get an extension — a context with a `vector` column
needs `pgvector/pgvector:pg17`. Override `InitializeDatabase` to `Database.MigrateAsync()` when you want the
test to prove the migrations produce a working schema, rather than building it from the model.
`ConfigureDataSource` and `ConfigureNpgsql` take the same hooks as `AddPostgresDbContextFactory`, so a type
plugin registered in production is registered here too.

Run without Docker by filtering: `dotnet test --filter "Category!=Integration"`.

## Related packages

- [MagicCSharp.Testing](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing/README.md)
  — `FakeTimeProvider`, `FakeKeyGen`, `SyncEventDispatcher`, without Docker
- [MagicCSharp.Data.EntityFramework](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.EntityFramework/README.md)
  — the repositories under test

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
