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

**The repository layout, offered as an option** — the structure MagicDoor runs its backend on: several
services in one repository, each with its own solution, sharing a set of libraries. Entirely opt-in; nothing
in the packages reads it. See [docs/repository-layout.md](docs/repository-layout.md).

**`MagicCSharp.Cli`, a .NET global tool** providing `mcs`:

```bash
dotnet tool install -g MagicCSharp.Cli
mcs init --prefix Acme
mcs create-app --name Shop --database shop
```

Pin it per repository with a tool manifest so a team runs one version:
`dotnet tool install MagicCSharp.Cli`, commit `.config/dotnet-tools.json`, and teammates
`dotnet tool restore` then use `dotnet mcs`. `dotnet new magiccsharp-repo` scaffolds that manifest.

The commands were single-file `dotnet run` scripts. Moving them into one project removed about 860 lines of
copy-pasted helpers — `TemplateResolver` alone lived in six files — and made them testable: **50 tests**
where there were none. `dotnet tool update` replaces a hand-written installer and update check, so
`install.sh` and the vendored `tools/` directory are gone.

Templates are embedded in the tool rather than installed as loose files, so there is no path to resolve and
nothing to go missing.

**Team-owned templates.** Every file the generators write comes from a `.hbs` template, and a team can
replace any single one without forking the rest — `.magiccsharp/templates/` in the repository wins over the
built-ins, per file, so an override taken today does not stop you receiving improvements to the eighteen you
did not touch.

```bash
mcs templates list                        # every template, and which layer provides it
mcs templates eject Entities/dal.cs.hbs   # copy one in to customise
git add .magiccsharp                      # commit it and the team has it
```

`eject` prints that commit command, and warns instead when the path is git-ignored — an override nobody
committed works for whoever wrote it and reaches no one, which can go unnoticed for a long time.

Across several repositories, keep the templates in a repository of their own and add it as a submodule at
`.magiccsharp/templates`, so house style is defined once —
[docs/template-overrides.md](docs/template-overrides.md) has the full flow. The directory comes from
`"templates"` in `magiccsharp.json`; `""` disables overrides.

**`MagicCSharp.Templates`** — a `dotnet new` template, for teams who would rather commit the tooling than
install it:

```bash
dotnet new install MagicCSharp.Templates
dotnet new magiccsharp-repo -n Acme
```

Lays down the configuration, `Apps/`, `Libs/` and a tool manifest pinning `MagicCSharp.Cli`, with the prefix
substituted everywhere. `--MagicCSharpVersion` and `--TargetFramework` are parameters, and the release script
keeps the pinned version in step with what it publishes, so a generated repository never references a version
that predates its own tooling.

**Tooling** — `tools/`, single-file .NET programs run with `dotnet run`, needing only the .NET 10 SDK. See
[tools/README.md](tools/README.md).
- `InitRepo` sets a repository up: `magiccsharp.json`, `Directory.Build.props`, central package management,
  the all-projects solution, `Apps/` and `Libs/`. Skips whatever already exists, so it composes with a
  repository that is already running.
- `CreateApp` creates a service — host project, its own solution, and the pair of data projects that keep
  repository contracts separate from their Entity Framework implementation. `--no-database` for a service
  that owns no tables. Picks a local port no other service has claimed.
- `CreateAppLib` creates a domain inside a service: `Default` for use cases, `Models` for entities, `Tests`.
  Wires `Default` to `Models`, never the reverse.
- `CreateLib` creates a shared library under `Libs/`; dots in the name nest directories.
- `AddEntity` writes the four files an entity needs across three projects, adds the `DbSet` and registers the
  repository, imports included.
- `ValidateConventions` — four rules for mistakes that compile. Exits non-zero, so it works as a CI step.
- `SyncAllProjects` regenerates the all-projects solution.

Nothing is ever overwritten, and re-running any tool produces no diff. Scripts read `magiccsharp.json` for
the namespace prefix, so they work in any repository using the layout rather than only in this one.

A repository scaffolded from empty with these — service, domain, shared library, entity — builds against the
published packages and serves a request, which is the test the tooling is held to.

**`MagicCSharp.App`** — the four packages a web service needs, wired in two calls:

```csharp
builder.AddMagicApp();
// ...
app.UseMagicApp(builder);
```

Use cases, `IClock`, Snowflake IDs, request-ID tracking, events, scheduling defaults, problem-details error
handling and the startup preflight, in the order they need. `MagicAppOptions` turns any piece off, and
registering a transport or schedule store first means the defaults step aside. A shortcut rather than a
layer: everything it calls is public on the package that owns it, so outgrowing the defaults means replacing
two lines with five, not working around a wrapper. Generated services use it, which took their `Program.cs`
from thirteen wiring lines to two.

**Works out of the box.** Three gaps where the framework defined something and then left the application to
supply the half that makes it work:

- `InMemoryScheduleStore`, and `AddMagicScheduling()` registering it alongside a file-system lock provider.
  `ScheduledBackgroundService` resolves `IScheduleStore` at run time and the packages shipped no
  implementation, so the advertised drift-free scheduling threw the moment it was used. Both defaults are
  single-machine and say so; registering your own wins.
- `AddMagicErrorHandling()` / `UseMagicErrorHandling()` in `MagicCSharp.AspNetCore`, mapping exceptions to
  RFC 7807 problem responses. The framework threw `NotFoundException` for a row that is not there and
  nothing turned it into a 404 — it reached the caller as a 500 with a stack trace. Also maps validation and
  argument failures to 400 and a cancelled request to 499, and outside Development returns a generic message
  while logging the detail, because an unhandled exception's message routinely carries a connection string.
- `HttpException` and friends — `BadRequestException`, `ConflictException`, `UnprocessableEntityException` —
  for when a use case genuinely means a status code.
- `GetOrThrow` on `IRepository`. `Update` and `Delete` throw `NotFoundException` for a missing key while
  `Get` returns null, so every endpoint fetching by id wrote its own throw to get a 404 out of the error
  handling. This is that line, once, naming the key in the exception.
- `AddMagicJsonConventions()`, on by default in `AddMagicApp`. The framework had one notion of how its
  types serialize, in `JsonDefaults`, and it was applied to Postgres jsonb columns and nowhere else — so the
  same enum was a name in the database and a number over HTTP, and `Optional<T>` did not round-trip through
  a request body at all, which is the one distinction that type exists to make. Only the two converters are
  applied, for both controllers and minimal APIs; `JsonDefaults` wholesale also sets
  `IgnoreReadOnlyProperties`, which would silently drop `Pagination.TotalPages` and every other computed
  property from a response.
- `{prefix}_VERIFY_CONNECTION` turns off the startup connection check from configuration. Opening a
  connection while registering is right by default — a wrong password should fail the deploy, not the first
  request — but there was no way to turn it off without editing the registration, which blocked booting a
  service in a test that replaces every repository.

Plus `ValidateServices()`, which resolves every registration at startup so a miswired dependency fails the
deploy rather than the first request that needs it. Generated apps get all of this wired in.

**Tests** — 156, where there were none.

### Fixed

- **`AddMagicUseCases` picked an implementation with `FirstOrDefault`.** With two implementations of one
  interface it silently registered whichever reflection returned first — in a test project, often the double.
  It now throws and names both.
- **Discovery only saw assemblies .NET had already loaded.** It loads one the first time a type in it is
  touched, so a domain project holding nothing but event handlers — referenced by the host, used by nothing —
  was not there when the scan ran. The dispatch returned and no handler received it. Reading the reference
  graph does not fix it either: the compiler leaves a reference out of the compiled metadata when no type from
  it is used, which is exactly that project. `ApplicationAssemblies` now loads what is deployed next to the
  executable, and both use-case and event-handler discovery go through it.
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
- **`mcs create-domain` left the new domain unreferenced by its service.** Nothing failed — the projects
  built and the solution opened — but the assembly was not deployed with the app, so its use cases were never
  registered. It now adds the reference, as `create-app` already does for the data projects.
- **`mcs add-entity` wrote every repository registration at sixteen spaces** instead of eight, with a blank
  line between each. `InsertBefore` spliced at the anchor rather than at the start of its line, so the
  caller's indentation was added to the anchor's own. Every generated repositories module carried it.
- **The domain project template composed its own assembly name** as `{prefix}.Libraries.{name}`, leaving the
  service out, so two services with a same-named domain both produced `Acme.Libraries.Domains.Orders`.

### Changed

- **`mcs create-domain` and `add-entity` take a service name.** `--solution Shop` rather than
  `--solution Acme.Shop.slnx`, and no flag at all when the repository has one service; with several and no
  flag they list them rather than guessing. Full paths still work.
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

Three scripts remain unported: `AddEvent`, `AddLib`, and `GenerateAssemblyCatalog`. The last matters once a
service spans many projects — .NET loads an assembly only when one of its types is first touched, so use
cases in a project the host never references go unregistered. Touch one type per project at startup until it
lands.

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
