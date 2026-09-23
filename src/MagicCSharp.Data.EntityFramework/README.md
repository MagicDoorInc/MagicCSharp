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

Each takes `(IDbContextFactory<TContext>, TimeProvider, ILoggerFactory)`. The soft-delete and search bases
include pagination. A derived repository supplies `CreateDal` and `ApplyFilter`; everything else is
inherited.

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
- Do not use the `required` keyword on columns — `From()` and `Apply()` construct the row and cannot satisfy
  it. The primary key on the base class is the one exception. `mcs validate` checks both of these.
- `[Column("snake_case")]`, `[Table("snake_case")]`, and `[Index]` on foreign keys and anything the filter
  narrows on.
- `[StringLength(n)]` for `VARCHAR(n)`, `[Precision(p, s)]` for a decimal.

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
`MagicDbContextFactory<TContext>` in `MagicCSharp.Data.Postgres` is one line. Then:

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
