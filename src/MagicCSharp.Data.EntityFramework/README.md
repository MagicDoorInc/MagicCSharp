# MagicCSharp.Data.EntityFramework

Entity Framework Core implementation of the repository contracts in `MagicCSharp.Data`. Derive from a base
class, supply the two things only you know — how to build a row and what the filter means — and inherit
create, read, update, delete, counts, pages, soft delete and search, each translating to SQL.

Add it to the data project, the one that owns the `DbContext`, the DALs and the migrations. The domain
project references `MagicCSharp.Data` and never this. Provider-agnostic: add
[MagicCSharp.Data.Postgres](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.Postgres/README.md)
for PostgreSQL wiring, or configure any EF provider yourself.

```bash
dotnet add package MagicCSharp.Data.EntityFramework
```

## The three types per entity

| Type | Role |
|---|---|
| `Order` | What the application passes around. Immutable record. |
| `OrderEdit` | The writable fields. `Order` derives from it, so an entity is accepted wherever an edit is. |
| `OrderDal` | The row. EF attributes, and the mapping in `ToEntity()` / `Apply()`. |

The split is what keeps EF attributes and column names out of business logic, and what makes "these fields
can be written" a compile-time fact rather than a convention. `OrderDal` lives in the data project, and
`Order` does not know it exists — the arrow points from storage toward the domain, never back.

## Base classes

Pick by key shape, then by what the entity needs:

| | Id (Snowflake) | Key (string) |
|---|---|---|
| CRUD | `BaseIdRepository` | `BaseKeyRepository` |
| + pagination | `BaseIdPaginatedRepository` | `BaseKeyPaginatedRepository` |
| + soft delete | `BaseIdSoftDeleteRepository` | `BaseKeySoftDeleteRepository` |
| + search | `BaseIdSearchRepository` | `BaseKeySearchRepository` |

Each takes `RepositoryServices<TContext>` — the context factory, `IKeyGenService`, `TimeProvider` and
`ILoggerFactory` in one constructor parameter — or, as before, `(IDbContextFactory<TContext>, TimeProvider,
ILoggerFactory)`. The soft-delete and search bases include pagination. A derived repository supplies `ApplyFilter`,
and `CreateDal` when it is built the old way; everything else is inherited. With
[MagicCSharp.Generators](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Generators/README.md)
the compiler can write both, and the DAL's mapping ([below](#generated-dal-mapping)).

## A worked entity

The row, deriving from `BaseIdDal` (or `BaseKeyDal`), which already declares the primary key column with
`DatabaseGeneratedOption.None` — the id comes from `IKeyGenService` before the insert, not from a sequence —
and the `created` and `updated` columns:

```csharp
[Table("orders")]
[Index(nameof(CustomerId))]
public class OrderDal : BaseIdDal<Order, OrderEdit>
{
    [Required]
    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Required]
    [Column("total")]
    [Precision(18, 2)]
    public decimal Total { get; set; }

    [Required]
    [Column("status")]
    public OrderStatus Status { get; set; }

    public override Order ToEntity()
    {
        return new Order
        {
            Id = Id,
            CustomerId = CustomerId,
            Total = Total,
            Status = Status,
            Created = Created,
            Updated = Updated,
        };
    }

    // Assign every writable field unconditionally. The repository asks the change tracker afterwards
    // whether anything actually changed.
    public override void Apply(OrderEdit edit)
    {
        CustomerId = edit.CustomerId;
        Total = edit.Total;
        Status = edit.Status;
    }

    public static OrderDal From(OrderEdit edit, long id)
    {
        var dal = new OrderDal { Id = id };
        dal.Apply(edit);
        return dal;
    }
}
```

The repository:

```csharp
public class OrdersEfRepository(
    IDbContextFactory<MagicShopContext> contextFactory,
    IKeyGenService keyGenService,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseIdPaginatedRepository<MagicShopContext, OrderDal, Order, OrderFilter, OrderEdit>(
        contextFactory, timeProvider, loggerFactory),
      IOrdersRepository
{
    protected override OrderDal CreateDal(OrderEdit edit)
    {
        return OrderDal.From(edit, keyGenService.GetId());
    }

    protected override IQueryable<OrderDal> ApplyFilter(IQueryable<OrderDal> query, OrderFilter filter)
    {
        query = query.ApplyNullableValueFilter(filter.CustomerId, x => (long?)x.CustomerId);
        query = query.ApplyNullableValueFilter(filter.Status, x => (OrderStatus?)x.Status);
        query = query.ApplyComparableRangeFilter(filter.Created, x => (DateTimeOffset?)x.Created);

        return query;
    }
}
```

And the registration, next to the context factory:

```csharp
services.AddPostgresDbContextFactory<MagicShopContext>(configuration);   // MagicCSharp.Data.Postgres
services.AddScoped<IOrdersRepository, OrdersEfRepository>();
```

Scoped is what `mcs add-entity` writes. A repository opens a fresh context per operation and holds no state,
so singleton is equally correct; scoped simply matches the use cases that depend on it.

### Rules for a DAL

- Configure the schema with attributes on the DAL, not in `OnModelCreating`. The row and its mapping are
  then one file.
- `[Required]` on every non-nullable column. Without it EF infers a nullable column and the database stops
  enforcing what the type says.
- Use the `required` keyword only on a column `From()` sets once and `Apply()` leaves alone — the key, a parent id
  that never changes. `Apply()` cannot satisfy it for any other column. `mcs validate` checks both of these.
- `[Column("snake_case")]`, `[Table("snake_case")]`, and `[Index]` on foreign keys and anything the filter
  narrows on.
- `[StringLength(n)]` for `VARCHAR(n)`, `[Precision(p, s)]` for a decimal.

## Generated DAL mapping

Most of the DAL above is copying: `ToEntity`, `Apply` and `From` assign each property to the one of the same name.
With [MagicCSharp.Generators](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Generators/README.md)
referenced, make the DAL `partial` and write only its columns:

```csharp
[Table("shipments")]
public partial class ShipmentDal : BaseIdDal<Shipment, ShipmentEdit>, IDalDeleted
{
    [Column("tenant_id")]
    public required long TenantId { get; set; }

    [Required]
    [Column("status")]
    public ShipmentStatus Status { get; set; }

    [Column("carrier_id")]
    public long? CarrierId { get; set; }

    [Required]
    [Column("tracking_code")]
    public string TrackingCode { get; set; } = "";

    [Column("delivered")]
    public DateTimeOffset? Delivered { get; set; }

    [Column("deleted")]
    public DateTimeOffset? Deleted { get; set; }
}
```

The compiler writes each method the DAL does not declare, matching properties by name and identical type, nullability
included, inherited and `[NotMapped]` ones too:

- `ToEntity()` sets every entity property from the DAL (MCS1201 names one with no column).
- `Apply(edit)` sets every edit property, except a `required`, `[Key]` or `Id`/`Key` column, which never changes
  after the insert (MCS1202).
- `static From(edit, id)` (or `From(edit, key)`) sets the id and exactly those set-once columns, then calls `Apply`
  (MCS1203 names a `required` column the edit has nothing for). A `required` column here is one `From` sets once —
  a parent id or tenant that never changes — and `mcs validate` accepts it in a `partial` DAL whose `From` is
  generated.

`From` is written only for a DAL that asks for it: one that writes neither `ToEntity` nor `Apply` by hand, or one that
lists `IDalFromEdit<TDal, TEdit>` itself. A `partial` DAL that maps by hand without `IDalFromEdit`, as a DAL written
before 1.1 does, gets no `From` and no build error about one; the generator leaves it as it is.

An id DAL whose `From` is generated, or that writes its own public `From` and nothing else by hand, also gets
`IDalFromEdit`, so the base's default `CreateDal` builds rows through `From` with an id from `IKeyGenService`. The
repository then needs neither method:

```csharp
public partial class ShipmentsRepository(RepositoryServices<TestDbContext> repositoryServices)
    : BaseIdSoftDeleteRepository<TestDbContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices)
{
}
```

`ApplyFilter` is generated from the filter's field names ([MagicCSharp.Data](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data/README.md#generated-filters)).
A repository built the old way, without `RepositoryServices`, has no `IKeyGenService` and keeps its own `CreateDal`;
the default throws naming the DAL when it has no `From`.

**When to write it by hand:** a column whose value converts — JSON, a value object, a computed column — needs the
method that touches it written by hand; the generator still writes the other mapping method, and `From` when the DAL
lists `IDalFromEdit<TDal, TEdit>`. A `[NotMapped]` property of the
entity's name and type can carry a conversion instead, and then nothing is hand-written. A key DAL's `CreateDal` stays
yours, because keys are often natural. To leave the generated shape, write the three methods (copy them from the
generated file) and drop `partial`.

### Registration by convention

```csharp
services.AddPostgresDbContextFactory<MagicShopContext>(configuration);   // MagicCSharp.Data.Postgres
services.AddRepositoriesFrom<MagicShopContext>();
```

`AddRepositoriesFrom<TContext>` scans the context's assembly and registers each repository class under every
non-generic interface it implements that extends `IRepository<,,,>`, scoped unless you pass a lifetime, plus
`RepositoryServices<TContext>`. A line written by hand before it wins: an interface
already registered is skipped. Two classes implementing one repository interface throw.
It does not register the context factory.

## What the base classes handle

Batch writes that fail as a unit rather than half-applying. Re-reading after create and update, so
navigation properties are populated rather than silently empty. `Created` and `Updated` stamped from
the `TimeProvider`, and `Updated` only when something actually changed, so a no-op write does not look like an edit
in an audit trail. Reads untracked and writes tracked, so a read-only query does not pay to snapshot every
row. A context per operation from the factory, so a repository is safe to hold as a singleton and safe to
call concurrently.

## What you can override

| Member | Default | Override when |
|---|---|---|
| `GetQuery(context)` | the set | `ToEntity` reads navigation properties — `Include` them here so they are loaded rather than silently empty |
| `ApplyOrder(query)` | newest first, by id | reads should come back in another order |
| `GetNotFoundException(id)` | `NotFoundIdException(id, "Order")` | callers should be able to catch this entity's absence in particular |
| `GetDbSet(context)` | `context.Set<OrderDal>()` | the DAL is not the context's default set for its type — rare |
| `CreateDal(edit)` | the DAL's `From` with an id from `IKeyGenService` | a new row needs more than the edit and an id |
| `GetQueryNoTracking(context)` | `GetQuery` as no-tracking | almost never |

Every method is `virtual`, so a repository that needs to do something unusual on `Create` can, while
still inheriting everything else.

## Hooks

Called inside the write, before `SaveChanges`, with the same context — so what a hook adds or removes is
saved in the same transaction as the row:

| Hook | When |
|---|---|
| `AfterDalCreatedHook(dal, edit, context)` | after `CreateDal`, before the row is added |
| `AfterDalApplyHook(dal, edit, context)` | after `Apply` on an update |
| `AfterDalDeleteHook(dal, context)` | after the row is removed |
| `AfterDalSoftDeleteHook(dal, context)` | after `Deleted` is stamped (soft-delete bases) |

This is where child rows belong. An order's lines arrive on the edit; the hook keeps the `order_lines`
table in step, and the DAL's `Apply` stays a plain column mapping:

```csharp
public record OrderLineEdit
{
    public long? Id { get; init; }              // null for a new line
    public required long ProductId { get; init; }
    public required int Quantity { get; init; }
}

public record OrderEdit
{
    ...
    public List<OrderLineEdit> Lines { get; init; } = [];
}
```

```csharp
protected override void AfterDalCreatedHook(OrderDal dal, OrderEdit edit, MagicShopContext context)
{
    foreach (var orderLineEdit in edit.Lines)
    {
        context.OrderLines.Add(new OrderLineDal
        {
            Id = keyGenService.GetId(),
            OrderId = dal.Id,
            ProductId = orderLineEdit.ProductId,
            Quantity = orderLineEdit.Quantity,
        });
    }
}

protected override void AfterDalApplyHook(OrderDal dal, OrderEdit edit, MagicShopContext context)
{
    var existingLines = context.OrderLines.Where(x => x.OrderId == dal.Id).ToList();
    var keptLineIds = new HashSet<long>();

    foreach (var orderLineEdit in edit.Lines)
    {
        var existingLine = orderLineEdit.Id == null
            ? null
            : existingLines.FirstOrDefault(x => x.Id == orderLineEdit.Id.Value);

        if (existingLine == null)
        {
            context.OrderLines.Add(new OrderLineDal
            {
                Id = keyGenService.GetId(),
                OrderId = dal.Id,
                ProductId = orderLineEdit.ProductId,
                Quantity = orderLineEdit.Quantity,
            });
            continue;
        }

        existingLine.ProductId = orderLineEdit.ProductId;
        existingLine.Quantity = orderLineEdit.Quantity;
        keptLineIds.Add(existingLine.Id);
    }

    context.OrderLines.RemoveRange(existingLines.Where(x => !keptLineIds.Contains(x.Id)));
}
```

Child ids come from `keyGenService.GetId()` like every other id — never from an identity column.

## MagicDbContext

Derive your context from it for two conventions:

```csharp
public class MagicShopContext(DbContextOptions<MagicShopContext> options) : MagicDbContext(options)
{
    public DbSet<OrderDal> Orders => Set<OrderDal>();
    public DbSet<OrderLineDal> OrderLines => Set<OrderLineDal>();
}
```

**Enums are stored as their names**, including inside JSON columns, so inserting a member in the middle of
an enum does not change the meaning of rows already written; only renaming a member is then a migration.
**`DateTimeOffset` is normalized to UTC before it is written**, so a value does not come back with a
different offset than it went in with — and Npgsql, which rejects a non-zero offset on a `timestamptz`,
does not throw on a developer's machine that CI never sees.

## Migrations

`dotnet ef` builds the context outside your DI container, so it needs a design-time factory —
`MagicDbContextFactory<TContext>` in `MagicCSharp.Data.Postgres` is one line. With
[MagicCSharp.Generators](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Generators/README.md)
it is none: name the database on the context, and the compiler writes `MagicShopContextFactory` deriving from
`MagicDbContextFactory<MagicShopContext>`:

```csharp
[MigrationsDatabase("shop")]
public class MagicShopContext(DbContextOptions<MagicShopContext> options) : MagicDbContext(options)
```

A context with its own `IDesignTimeDbContextFactory` in the project gets none, which is also how to leave the
generated one: write the class. A codebase with its own factory base names it with the MSBuild property
`MagicCSharpDesignTimeContextFactoryBase` (a generic class with one type parameter whose constructor takes the database
name first). `MigrationsDatabaseAttribute.NameOf(typeof(MagicShopContext))` reads the name, which is how the migration
check in [MagicCSharp.Testing.Database](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing.Database/README.md#migrations-upgrade-from-the-last-release)
finds its database. Then:

```bash
dotnet ef migrations add CreateOrders --project Apps/Shop/Data/Data.EntityFramework
dotnet ef database update --project Apps/Shop/Data/Data.EntityFramework
```

## Testing it

A repository is mostly translation — a filter into SQL, a row into an entity — and the in-memory and SQLite
providers translate differently from Postgres.
[MagicCSharp.Testing.Database](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing.Database/README.md)
runs repository tests against a real PostgreSQL in a container, one container per suite. The base classes
here are tested that way: fifty tests over create, filter, update, delete, pagination, soft delete and the
search column.

## Related packages

- [MagicCSharp.Data](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data/README.md)
  — the contracts this implements, and the filter helpers
- [MagicCSharp.Data.Postgres](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.Postgres/README.md)
  — pooled context factory, design-time factory, UTC interceptor

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
