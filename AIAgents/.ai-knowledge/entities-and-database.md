# Entities and Database

This guide owns entities, DALs, repositories, filters and migrations. Create them with `mcs add-entity`
(`project-tooling.md`), then fill them in as below.

## An entity is three types in one file

```csharp
public record Charge : ChargeEdit, IMagicEntity, IIdEntity      // what comes back
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record ChargeEdit                                         // what you can write
{
    public required long LeaseId { get; init; }
    public required ChargeType Type { get; init; }
    public required decimal Amount { get; init; }
    public DateTimeOffset? Paid { get; init; }
}

public class ChargeFilter                                        // what you can query by
{
    public List<long>? Ids { get; init; }
    public List<long>? LeaseIds { get; init; }
    public bool? IsPaid { get; init; }
    public ComparableRange<DateOnly>? DueDate { get; init; }
}
```

- The entity lives in `{Domain}/Models/Entities/`. An enum it persists gets its own file beside it.
- A null filter property means "do not narrow on this". Lists for many values, `ComparableRange<T>` for
  ranges, `bool?` for yes/no.
- **Nullability is a claim about the domain.** A column is nullable only when "not yet" or "not applicable"
  is a real state (`Paid` before payment). Everything else is required.

## Keys

- **A Snowflake `long Id`** from `IKeyGenService` for ordinary entities. It is assigned before the insert, so
  the caller knows the id without a round trip.
- **Another entity's id** for a one-to-one: `LateFeePolicy`'s id is its property's id.
- **A string `Key`** (`--use-key`) for a natural or computed key. Make it deterministic and lowercase with
  underscores: a notification is keyed `welcome_{leaseId}`, so queueing it twice finds the first one.

## The DAL: two-step construction

The DAL is the row. `From()` builds it with the key and the columns that never change; `Apply(edit)` sets
everything else, so create and update share one mapping.

```csharp
public static ChargeDal From(ChargeEdit edit, long id)
{
    var dal = new ChargeDal
    {
        Id = id,
        LeaseId = edit.LeaseId,     // a charge never moves to another lease
        Type = edit.Type,
    };
    dal.Apply(edit);
    return dal;
}

public override void Apply(ChargeEdit edit)
{
    Amount = edit.Amount;           // never LeaseId or Type: those are set once, above
    DueDate = edit.DueDate;
    Paid = edit.Paid;
}
```

- A column `From()` sets once and `Apply()` leaves alone is `required`; every other non-nullable column is
  `[Required]`. `mcs validate` checks both.
- Every column has `[Column("snake_case")]`; every string has `[StringLength(n)]`. Enums need no length —
  `MagicDbContext` stores every enum by name.
- `ToEntity()` maps every column back.

## Foreign keys

Every reference to another table in the same context is a declared foreign key, with a navigation property and
an explicit delete behaviour, after all the column properties:

```csharp
[ForeignKey(nameof(LeaseId))]
[DeleteBehavior(DeleteBehavior.Cascade)]         // required reference: the child goes with the parent
public virtual LeaseDal Lease { get; set; } = null!;

[ForeignKey(nameof(LateFeeForChargeId))]
[DeleteBehavior(DeleteBehavior.SetNull)]         // optional reference: keep the child, drop the link
public virtual ChargeDal? LateFeeForCharge { get; set; }
```

The parent declares the inverse collection with `[InverseProperty]`. EF creates an index for each foreign key;
check the migration.

## Repositories

`mcs add-entity` writes the interface in `Data.Models/Repositories/` and the EF implementation in
`Data.EntityFramework/Repositories/`. The implementation overrides two things: `CreateDal` and `ApplyFilter`.

```csharp
protected override IQueryable<ChargeDal> ApplyFilter(IQueryable<ChargeDal> query, ChargeFilter filter)
{
    query = query.ApplyListFilter(filter.LeaseIds, x => (long?)x.LeaseId);
    query = query.ApplyNullableValueFilter(filter.IsPaid, x => (bool?)(x.Paid != null));
    query = query.ApplyComparableRangeFilter(filter.DueDate, x => (DateOnly?)x.DueDate);
    return query;
}
```

- Use the `Apply*Filter` helpers, not hand-written `.Where(...)`.
- **No business logic in a repository**: no status checks, no state transitions, no scope rules. The use case
  decides; the repository stores.
- **No bespoke methods** when `Get`, `Create`, `Update` or `Delete` already express the operation. Batch
  forms (`Create(IReadOnlyList<TEdit>)`, `Get(IReadOnlyList<TKey>)`) exist — use them instead of loops.
- Change a row with `repository.Update(entity with { Paid = timeProvider.GetUtcNow() })`.

## Migrations

One migration per change, named for what it does, generated at the end
(`dotnet ef migrations add ... --project Apps/{Service}/Data/Data.EntityFramework`). Read the generated file.
Do not add database-level defaults; the code owns defaults. Tests build their schema from the migrations.
