# Changelog

Notable changes to MagicCSharp. All packages share one version number and ship together.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## Unreleased — 0.1.0

Release with `./scripts/publish-all.sh --minor`.

The framework and MagicDoor's backend were split apart in June and diverged: every shared file differed. This
release brings the backend's improvements across, and splits the five packages into eleven so a consumer takes
only what it uses.

**This release is breaking.** See [Migrating](#migrating-from-0011) below.

### Added

**Packages** — six new, so no package forces a dependency you did not ask for:

| Package | Holds |
|---|---|
| `MagicCSharp.AspNetCore` | Request-ID middleware, moved out of core |
| `MagicCSharp.Scheduling` | Background services and schedule policies, moved out of core |
| `MagicCSharp.Data.EntityFramework` | Repository base classes, DALs and `MagicDbContext`, moved out of `MagicCSharp.Data` |
| `MagicCSharp.Data.Postgres` | Pooled context factory, design-time factory, UTC command interceptor |
| `MagicCSharp.Testing` | Test doubles for the framework's own seams |
| `MagicCSharp.Testing.Database` | Repository tests against PostgreSQL in Testcontainers |

**Repositories**
- `ISoftDeleteRepository`, `IPaginatedRepository` and `ISearchRepository` — a repository declares which of
  these an entity supports, instead of every repository carrying every capability.
- Six base classes covering soft delete and search for both key shapes.
- `GetKeys(filter)` for when you only need which entities matched, not the entities.
- Batch `Update(IReadOnlyDictionary<TKey, TEdit>)` and `Update(IReadOnlyList<TEntity>)`. Both fail as a unit
  rather than half-applying.
- `Delete(filter)`, which deletes in the database rather than loading the rows first.
- `Delete` now returns the number of rows affected.
- Reads run untracked; writes keep tracking. A read-only query no longer pays to snapshot every row.

**Entity Framework**
- `MagicDbContext` — stores enums by name, including inside JSON columns, so inserting a member in the middle
  of an enum does not silently change the meaning of rows already written. Normalizes `DateTimeOffset` to UTC
  before writing.
- `BaseIdDal` and `BaseKeyDal` carry the `[Key]`/`[DatabaseGenerated(None)]`/`[Column]` declaration that was
  repeated in every DAL.
- `IDalDeleted` and `IDalSearchField`.

**PostgreSQL**
- `AddPostgresDbContextFactory` — pooled factory with pool sizing, timeouts and retry in
  `PostgresConnectionOptions`. Opens one connection during registration, so a wrong host or password fails at
  startup rather than on the first request that needs the database.
- `MagicDbContextFactory<TContext>` for `dotnet ef`, which builds the context outside your DI container.
- `UtcDateTimeOffsetCommandInterceptor` — normalizes `DateTimeOffset` command parameters to UTC. `MagicDbContext`
  already does this on save, but save only sees entities; query predicates, `ExecuteUpdate`, `ExecuteDelete` and
  raw SQL bind parameters without going near the change tracker. Npgsql rejects a non-zero offset on a
  `timestamptz` parameter, so one of those throws on a developer's machine and not in CI, which runs in UTC.

**Testing** — the repo had nothing here before.
- `FakeClock`, and `FakeKeyGen` whose ids are derived from it, so ids and timestamps agree inside a test.
- `SyncEventDispatcher` — runs handlers inline and records what was dispatched, so you can assert without
  sleeping.
- `InMemoryDistributedLockProvider`, re-entrant for the duration of an inline dispatch so a handler
  re-acquiring the emitter's lock does not deadlock in a test on something that works in production.
- `TrackingDistributedLockProvider`, for asserting *what* was locked rather than that it serialized.
- `TestRepositoryBase` — one container per suite, a logical database per test class, schema created once,
  tables truncated between tests.

**Infrastructure**
- `IKeyGenService.GetKey(length)`, `IsValidKey` and `IsValidId`. `IDalKey` and `BaseKeyRepository` existed for
  string-keyed entities but nothing generated the key. The alphabet is Base58 — no `0`/`O`, no `I`/`l`.
- `Optional<T>` — distinguishes "the caller did not mention this field" from "the caller set it to null",
  which a partial update cannot express otherwise.
- `AddImplementationsOf<T>` and `AddImplementationsOfBase<T>` — convention registration for any marker
  interface, with optional `Lazy<T>`, generalizing the use-case-only version.
- `IDeletedEntity`.
- `JsonDefaults` sets `AllowOutOfOrderMetadataProperties`; `jsonb` does not preserve property order, so a
  polymorphic type's discriminator can come back anywhere in the object.

**Tooling** — `tools/`, single-file .NET programs run with `dotnet run`. See [tools/README.md](tools/README.md).
- `AddEntity` writes the four files an entity needs across three projects, adds the `DbSet` and registers the
  repository, imports included. Skips existing files rather than overwriting your edits.
- `ValidateConventions` — four rules for mistakes that compile. Exits non-zero, so it works as a CI step.
- `SyncAllProjects` regenerates the all-projects solution.
- Scripts read `magiccsharp.json` at the repo root for the namespace prefix, so they work in any repository
  using the layout rather than only in this one.

**Tests** — 47, where there were none.

### Fixed

- **`AddMagicUseCases` picked an implementation with `FirstOrDefault`.** With two implementations of one
  interface it silently registered whichever reflection returned first — in a test project, often the double.
  It now throws and names both.
- **`AddMagicUseCases` only saw assemblies already loaded.** .NET loads an assembly the first time one of its
  types is touched, so use cases in a project the host had not referenced were never registered and failed at
  resolution time. Documented on the method, with how to make a project visible.
- **`LocalEventDispatcher` blocked while Kafka and SQS do not.** The promise is that swapping the registration
  changes nothing else, but a handler that re-entered a lock its emitter held worked locally and deadlocked
  after the switch to Kafka. It is now fire-and-forget with in-flight tracking and a drain on process exit,
  matching the distributed transports. Use `SyncEventDispatcher` from `MagicCSharp.Testing` where a test needs
  handlers to have finished.
- **`ScheduledBackgroundService` logged its next run from the wall clock** while scheduling from the injected
  `IClock`, so under a test clock the log reported a time the service was not working from.
- **`MagicCSharp` pinned `Microsoft.AspNetCore.Http.Abstractions` 2.2.0**, whose last release was ASP.NET Core
  2.2. The middleware moved to `MagicCSharp.AspNetCore`, which uses a framework reference.
- Assembly scanning survives a `ReflectionTypeLoadException` instead of aborting on one unloadable assembly.

### Changed

- `IRepository<TEntity, TFilter, TEdit>` and `IKeyRepository<TEntity, TFilter, TEdit>` are replaced by one
  `IRepository<TEntity, TKey, TEdit, TFilter>` where `TKey : IEquatable<TKey>`.
- Key generation moved from `MagicCSharp.Data` to `MagicCSharp` — a key generator is not a data-access
  concern, and `MagicCSharp.Testing` should not pull in Entity Framework to offer a fake for it.
- Repository constructors take `ILoggerFactory` rather than `ILogger`, so each repository logs under its own
  concrete type rather than the base class.
- `IDalKey.Key` is get-only, matching `IDalId.Id`. The concrete DAL declares the setter its factory needs.
- Use-case registration no longer also registers the concrete type. Depend on the interface.
- `Delete` returns `Task<int>` rather than `Task`.
- Key-keyed repositories order by `Created` descending by default. Ordering by a random key is arbitrary.

### Not included

`MagicCSharp.Testing.Database` compiles but has no tests of its own yet — the ~4,500 lines of repository tests
in the backend have not been ported, so nothing yet proves the repository bases behave correctly against a live
container. That is the next piece of work.

Six scripts remain unported: `CreateApp`, `CreateAppLib`, `CreateLib`, `AddEvent`, `AddLib` and
`GenerateAssemblyCatalog`. A new service's project skeleton is still created by hand.

---

## Migrating from 0.0.11

**Repository interfaces.** Reorder the type arguments and add the key type:

```csharp
// before
public interface IOrderRepository : IRepositoryPaginated<Order, OrderFilter, OrderEdit> { }

// after
public interface IOrderRepository :
    IRepository<Order, long, OrderEdit, OrderFilter>,
    IPaginatedRepository<Order, OrderFilter> { }
```

`IKeyRepository<TEntity, TFilter, TEdit>` becomes `IRepository<TEntity, string, TEdit, TFilter>`.

**Repository base classes.** `BaseIdPaginationRepository` is now `BaseIdPaginatedRepository`, and the
constructor takes `ILoggerFactory`:

```csharp
// before
ILogger<OrderEfRepository> logger) : BaseIdPaginationRepository<...>(contextFactory, clock, logger)

// after
ILoggerFactory loggerFactory) : BaseIdPaginatedRepository<...>(contextFactory, clock, loggerFactory)
```

**Package references.** Add what the split moved out:

| Using | Now also needs |
|---|---|
| `BaseIdRepository`, `BaseDal`, `MagicDbContext` | `MagicCSharp.Data.EntityFramework` |
| `UseRequestId()` | `MagicCSharp.AspNetCore` |
| `ScheduledBackgroundService` | `MagicCSharp.Scheduling` |

**Namespaces.**

| Was | Is |
|---|---|
| `MagicCSharp.Data.KeyGen` | `MagicCSharp.Infrastructure.KeyGen` |
| `MagicCSharp.Data.Dals` | `MagicCSharp.Data.EntityFramework.Dals` |
| `MagicCSharp.Data.Repositories` (base classes) | `MagicCSharp.Data.EntityFramework.Repositories` |
| `MagicCSharp.Middleware`, `MagicCSharp.Modules` (request ID) | `MagicCSharp.AspNetCore` |
| `MagicCSharp.BackgroundServices.Scheduling` | `MagicCSharp.Scheduling` |

The repository *interfaces* stay in `MagicCSharp.Data.Repositories`; only the implementations moved.

**`IKeyGenService`** gained four members. A hand-written implementation needs `GetKey`, `IsValidKey` and
`IsValidId`; `SnowflakeKeyGenService` and `FakeKeyGen` already have them.

**Duplicate use cases now throw at startup** rather than registering silently. If startup fails naming two
implementations, that resolution was already ambiguous — delete one, register it by hand, or use
`AddImplementationsOfBase<T>` if the interface really is a catalog.

**Local event dispatch no longer blocks.** A test that relied on handlers having finished when `Dispatch`
returned should use `SyncEventDispatcher` from `MagicCSharp.Testing`.

---

## 0.0.11 and earlier

Not recorded. `git log` is the history.
