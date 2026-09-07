# MagicCSharp.Data.EntityFramework

Entity Framework Core implementation of the repository contracts in `MagicCSharp.Data`. Provider-agnostic; add
`MagicCSharp.Data.Postgres` for PostgreSQL wiring.

## The three types per entity

| Type | Role |
|---|---|
| `Order` | What the application passes around. Immutable record. |
| `OrderEdit` | The writable fields. `Order` derives from it, so an entity is accepted wherever an edit is. |
| `OrderDal` | The row. EF attributes, and the mapping in `ToEntity()` / `Apply()`. |

The split is what keeps EF attributes and column names out of business logic, and what makes "these fields can
be written" a compile-time fact rather than a convention.

## Base classes

Pick by key shape, then by what the entity needs:

| | Id (Snowflake) | Key (string) |
|---|---|---|
| CRUD | `BaseIdRepository` | `BaseKeyRepository` |
| + pagination | `BaseIdPaginatedRepository` | `BaseKeyPaginatedRepository` |
| + soft delete | `BaseIdSoftDeleteRepository` | `BaseKeySoftDeleteRepository` |
| + search | `BaseIdSearchRepository` | `BaseKeySearchRepository` |

A derived repository supplies `CreateDal` and `ApplyFilter`; everything else is inherited.

## What the base classes handle

Batch writes that fail as a unit rather than half-applying. Re-reading after create and update so navigation
properties are populated rather than silently empty. Stamping `Updated` only when something actually changed,
so a no-op write does not look like an edit. A context per operation from the factory, so a repository is safe
to hold as a singleton.

## MagicDbContext

Derive your context from it for two conventions: enums stored as their names, so inserting a member in the
middle of an enum does not change the meaning of rows already written; and `DateTimeOffset` normalized to UTC
before it is written, so a value does not come back with a different offset than it went in with.
