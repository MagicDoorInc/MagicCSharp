# MagicCSharp.Data

Repository contracts for a domain that should not know how it is stored. `IRepository<TEntity, TKey, TEdit,
TFilter>`, three opt-in capabilities, the pagination types, and the LINQ helpers that turn a filter into a
query. No persistence library — a domain project referencing this gets interfaces and nothing else.

Add it to the project that holds your entities and repository interfaces. The Entity Framework
implementation is
[MagicCSharp.Data.EntityFramework](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.EntityFramework/README.md),
with PostgreSQL wiring in
[MagicCSharp.Data.Postgres](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.Postgres/README.md)
— or implement the same interfaces over whatever store you have.

```bash
dotnet add package MagicCSharp.Data
```

## An entity is three records

What you can write, what comes back, and what you can query by:

```csharp
public record Order : OrderEdit, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

public record OrderEdit
{
    public required long CustomerId { get; init; }
    public required decimal Total { get; init; }
    public required OrderStatus Status { get; init; }
}

public class OrderFilter
{
    public long? CustomerId { get; init; }
    public OrderStatus? Status { get; init; }
    public ComparableRange<DateTimeOffset>? Created { get; init; }
}
```

`Order` derives from `OrderEdit`, so an entity is accepted wherever an edit is, and "these fields can be
written" is a compile-time fact rather than a convention. `Id`, `Created` and `Updated` are set by the
system and cannot be handed in on an edit. Entities keyed by an unguessable string implement `IKeyEntity`
instead of `IIdEntity`.

## The interface says what the entity supports

```csharp
public interface IOrdersRepository :
    IRepository<Order, long, OrderEdit, OrderFilter>,
    IPaginatedRepository<Order, OrderFilter>;
```

`TKey` is `long` for a Snowflake id or `string` for a key. Pagination, soft delete and search are
deliberately not on `IRepository`: a repository opts in by also implementing `IPaginatedRepository`,
`ISoftDeleteRepository` or `ISearchRepository`, so a caller can see from the interface which of them the
entity supports, and a small lookup table does not carry a paging method it will never need.

That interface is everything the domain sees. A use case takes `IOrdersRepository`; the class behind it
lives in the data project and the domain never references it.

## What IRepository gives you

| Member | |
|---|---|
| `Count(filter)` | Without loading rows |
| `GetKeys(filter)` | Just the keys, when that is all the caller needs |
| `Get(filter)` | The entities matching |
| `Get(keys)` | By several keys; missing ones are skipped, so the result may be shorter |
| `Get(key)` | One, or null |
| `Create(edit)` · `Create(edits)` | One, re-read so navigation properties are populated; or several in one round trip, returning keys in order |
| `Update(key, edit)` · `Update(edits)` | Apply an edit; or several in one round trip — nothing is written if any key is missing |
| `Update(entity)` · `Update(entities)` | Write back something read, edited in memory and saved whole |
| `Delete(key)` · `Delete(keys)` · `Delete(filter)` | Permanent. The filter form deletes in the database rather than loading first |

A write to a missing key throws `NotFoundException`. The batch forms fail as a unit rather than half-applying.
Deletes and batch updates return the number of rows affected.

```csharp
var order = await ordersRepository.GetOrThrow(orderId);   // NotFoundIdException(orderId, "Order") if it is not there
```

`Get(key)` returns null, which is right for the caller that has something to do about it and wrong for the
far more common one that does not. `GetOrThrow` makes reads agree with `Update` and `Delete`, and with
`MagicCSharp.AspNetCore` the exception is a 404 — so an endpoint that fetches by id has no null branch.

## One Get(filter), not a method per question

Almost every query is a filter. A method per question — `GetByCustomer`, `GetPendingSince`,
`GetLatestForCustomer` — grows a repository interface without bound and puts the same `WHERE` clause in
three places.

```csharp
var pendingOrders = await ordersRepository.Get(new OrderFilter
{
    CustomerId = customerId,
    Status = OrderStatus.Pending,
    Created = new ComparableRange<DateTimeOffset> { Start = timeProvider.GetUtcNow().AddDays(-7) },
});
```

A filter property is nullable, and null means "do not narrow on this". Add a property when a caller needs to
narrow on something new. In the implementation, these helpers turn each property into a predicate and skip it
when it is null:

| Helper | Narrows on |
|---|---|
| `ApplyNullableValueFilter(value, x => (long?)x.CustomerId)` | equality on a nullable struct |
| `ApplyStringNullableValueFilter(value, x => x.Name, StringFilterOperation.Contains)` | `Equals`, `Contains`, `StartsWith` or `EndsWith`, case-insensitive |
| `ApplyComparableRangeFilter(range, x => (DateTimeOffset?)x.Created)` | a `ComparableRange<T>`, with inclusive or exclusive ends |
| `ApplyListFilter(values, x => (long?)x.Id)` | membership in a list |
| `ApplyNavigationNullableFilter(values, x => x.Tags, tag => (long?)tag.Id)` | any related row's value being in a list |
| `ApplyIsDeletedFilter(isDeleted, x => x.Deleted)` | null: every row; false: live only; true: deleted only |
| `ApplyNullablePresenceFilter(isPresent, x => x.Delivered)` | null: every row; true: the column has a value; false: it is null |

The selector casts to the nullable type so the helper can compare against a possibly-null column. An empty
list deliberately matches nothing rather than everything: `Ids = []` from a caller that computed "no orders"
should return no orders.

Filter values are sent as SQL parameters, so one query plan serves every value.

Write a custom method when the filter genuinely cannot express the query — a join across several tables, a
window function, an aggregation, raw SQL for performance. Those are the exceptions, and the interface should
say so by having very few of them.

## Generated filters

With [MagicCSharp.Generators](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Generators/README.md)
referenced, most of a filter needs no code: a field named and typed after an entity property already says what it
narrows on. Make the repository `partial` and leave out `ApplyFilter`, and the compiler writes it, one helper call per
field. Make the filter `partial` and list `IEntityFilter<TEntity>`, and it writes `Matches(entity)`, the same rules in
memory, which `InMemoryRepository` in `MagicCSharp.Testing` uses — so a fake and the database cannot disagree.

```csharp
public partial class ShipmentFilter : IEntityFilter<Shipment>
{
    public List<long>? Ids { get; init; }                         // Id is in the list
    public long? CarrierId { get; init; }                         // CarrierId equals it
    public List<ShipmentStatus>? Statuses { get; init; }          // Status is in the list
    public bool? IsDelivered { get; init; }                       // Delivered has a value, or not
    public ComparableRange<DateTimeOffset>? Created { get; init; }
    public ComparableRange<DateTimeOffset>? DeliveredRange { get; init; }
    public string? TrackingCodeStartsWith { get; init; }          // fits no rule: custom

    private partial bool MatchesCustomFilter(Shipment entity)
    {
        return TrackingCodeStartsWith == null || entity.TrackingCode.StartsWith(TrackingCodeStartsWith, StringComparison.Ordinal);
    }
}

public partial class ShipmentsRepository(RepositoryServices<TestDbContext> repositoryServices)
    : BaseIdSoftDeleteRepository<TestDbContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices)
{
    private partial IQueryable<ShipmentDal> ApplyCustomFilter(IQueryable<ShipmentDal> query, ShipmentFilter filter)
    {
        var trackingCodeStart = filter.TrackingCodeStartsWith;
        if (trackingCodeStart == null)
        {
            return query;
        }

        return query.Where(shipmentDal => shipmentDal.TrackingCode.StartsWith(trackingCodeStart));
    }
}
```

The rules, by the field's name and type against the entity's properties:

| Field | Entity property | Means |
|---|---|---|
| a list of `V`, plural name (`Ids`, `Statuses`, `Categories`) | the singular (`Id`, `Status`, `Category`), `V` or `V?` | the value is in the list; an empty list matches nothing |
| `V?`, same name | `V` or `V?` | equal; a null value never matches |
| `bool? Is{X}` with no entity `Is{X}` | `{X}`, a nullable struct | `{X}` has a value (true) or not (false) |
| `ComparableRange<V>`, named `{X}` or `{X}Range` | `{X}`, `V` or `V?` | inside the range |

A null field narrows nothing. A field that fits no rule is custom: the generator declares
`ApplyCustomFilter` on the repository and `MatchesCustomFilter` on the filter, the compiler then requires both, and
each must read every custom field (MCS1301, MCS1304), so the two sides cannot drift apart. A field that fits a rule
needs a DAL column of the entity property's name and type (MCS1302, MCS1303). No attribute or setting changes a rule.

**When to write it by hand:** a query the rules cannot say goes in `ApplyCustomFilter`, not in a hand-written
`ApplyFilter`; write all of `ApplyFilter` only when most fields are custom. A filter without `IEntityFilter` gets no
`Matches`. To leave the generated shape, write `ApplyFilter` (or `Matches`) in the class — the generator stops when one
exists — and drop `partial` if nothing else is generated for it.

## Pages

```csharp
var page = await ordersRepository.Get(new PaginationRequest(pageSize: 20, page: 1), filter);

page.Items;        // this page
page.TotalCount;   // so a caller can render "page 3 of 12" without a second query
page.TotalPages;
```

`PaginationRequest` clamps to a minimum page of 1 and page size of 1, and defaults to 50 per page.
`new PaginationRequest(isDisabled: true)` returns everything, for an export.

## A port, not Entity Framework

The paved path is EF + Postgres. The port is not. DynamoDB for writes with Elasticsearch for queries can sit
behind the same `IOrdersRepository`, and the use case that calls it does not change. What changes is the
filter: on DynamoDB you can only add a property you can actually query — a key, an index — where
Elasticsearch can be far wider. The filter is where you admit what a store can do, and adding a property to
it is the moment to know which adapter you are stretching. That is not the AWS SDK leaking into a use case;
it is engineers knowing their access patterns.

## Soft delete and search

`ISoftDeleteRepository<TEntity, TKey, TFilter>` keeps the row and stamps `Deleted`, for anything a user can
delete by mistake, anything an audit trail references, and anything a foreign key still points at. It coexists
with `Delete` on purpose — soft delete for normal use, hard delete for a genuine purge. Soft-deleted rows still
come back from queries unless the filter excludes them; do that in the repository's own filter application so
callers cannot forget.

`ISearchRepository<TKey>` is free-text search without a search engine: one denormalized column of normalized
keywords per row, which the caller refreshes with `UpdateSearch(key, keywords)` whenever a contributing value
changes. `SearchText` does the normalization on both sides — lowercase, punctuation stripped, an optional
synonym map — so "St." in the query finds "Street" in the data if the map says they are the same. A row
matches when it contains every term, so more words narrow the result rather than widening it.

## Implementations

- [MagicCSharp.Data.EntityFramework](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.EntityFramework/README.md)
  — `BaseIdRepository` and friends: derive, supply `ApplyFilter` (or let the compiler write it), inherit the rest
- [MagicCSharp.Data.Postgres](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.Postgres/README.md)
  — pooled context factory, design-time factory, UTC interceptor
- [MagicCSharp.Testing.Database](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing.Database/README.md)
  — repository tests against a real PostgreSQL

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
