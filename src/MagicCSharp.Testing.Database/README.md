# MagicCSharp.Testing.Database

Repository integration testing against a real PostgreSQL, via Testcontainers. Requires Docker.

Split out from `MagicCSharp.Testing` on purpose: the fakes there have no heavy dependencies, and a project that
only wants `FakeClock` should not pull in Testcontainers, xUnit and Npgsql.

## Why a real database

A repository is mostly translation — a filter into SQL, a row into an entity. The in-memory and SQLite
providers translate differently from Postgres, so a test that passes against them proves the C# and not the
query, which is the half that breaks. Enum-as-string columns, `jsonb` round-trips and `timestamptz` offset
rules have no equivalent anywhere else.

## Cost control

One container for the whole suite. One logical database per test class, so classes run in parallel without
seeing each other's rows. Schema created once per database. Tables truncated between tests rather than
recreated.

## Use

```csharp
[Trait("Category", "Integration")]
public class OrderRepositoryTests : TestRepositoryBase<ShopContext>
{
    private OrderEfRepository Repository => new OrderEfRepository(DbContextFactory, Clock, KeyGen, NullLoggerFactory.Instance);

    [Fact]
    public async Task Create_assigns_created_and_updated_from_the_clock()
    {
        Clock.SetTime(2026, 3, 1);

        var order = await Repository.Create(new OrderEdit { UserId = 1 });

        Assert.Equal(Clock.Now(), order.Created);
    }
}
```

Override `ContainerImage` to match production, or to get an extension — a context with a `vector` column needs
`pgvector/pgvector:pg17`. Override `InitializeDatabase` to `Database.MigrateAsync()` when you want the test to
prove the migrations produce a working schema, rather than building it from the model.

Run without Docker by filtering: `dotnet test --filter "Category!=Integration"`.
