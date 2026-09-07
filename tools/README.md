# Tools

Scaffolding and linting for a repository built on MagicCSharp. Each tool is a single-file .NET program —
no project, no build step, dependencies declared inline with `#:package`. Requires the .NET 10 SDK; the
libraries themselves target net9.0.

```bash
dotnet run tools/AddEntity.cs -- --help
```

## Setup

The tools operate on a repository that follows the MagicCSharp layout, marked by a `magiccsharp.json` at its
root:

```json
{ "prefix": "Acme" }
```

The prefix is the namespace and solution-name root: `Acme.Shop.slnx`, `Acme.Shop.Data.EntityFramework`. Every
tool refuses to run without this file rather than guessing and scattering files into the wrong directories.

## Layout

```
magiccsharp.json
Acme.All.slnx                        every project, generated
Acme.Shop.slnx                       one service
Apps/
  Shop/
    Shop.App/                        controllers, host
    Shop.Domains/
      Orders/
        Default/                     use cases, event handlers
        Models/                      entities, edits, filters
        Tests/
    Data/
      Data.Models/                   repository interfaces
      Data.EntityFramework/          DALs, EF repositories, context, migrations
Libs/
  {Group}/Default/
```

The split that matters: **entities are owned by their domain**, persistence is service-level. `Order` lives
under `Orders/Models`; `OrderDal` and `OrdersEfRepository` live under `Data/`. A domain can be read without
reading how it is stored.

## Tools

### AddEntity.cs

Scaffolds an entity across the four files that have to agree about it.

```bash
dotnet run tools/AddEntity.cs -- --solution Acme.Shop.slnx --domain Orders --name Order --paginated
dotnet run tools/AddEntity.cs -- --solution Acme.Shop.slnx --domain Access --name ApiKey --use-key
```

| Flag | Meaning |
|---|---|
| `-s, --solution` | The service's solution file |
| `-d, --domain` | Owning domain; its Models project must exist |
| `-n, --name` | Entity name, PascalCase singular |
| `-p, --paginated` | Also give the repository page-at-a-time reads |
| `-k, --use-key` | Key by unguessable string rather than Snowflake id |

Writes `Order.cs` (entity, edit, filter), `IOrdersRepository.cs`, `OrderDal.cs` and `OrdersEfRepository.cs`,
adds the `DbSet` to the context and registers the repository — imports included.

**`--use-key` when the key is public.** A Snowflake id encodes the time it was issued and sits next to its
neighbours, so putting one in a URL leaks both when the record was created and roughly how many exist. A
random key leaks neither. Use it for invite links, webhook targets and API keys.

**Existing files are skipped, never overwritten** — regenerating over a file you have edited would throw the
edits away. Re-running is safe: registrations are not duplicated either.

The generated `ToEntity`, `Apply` and `ApplyFilter` have `TODO`s. That is the intent: only you know the
columns.

Afterwards:
```bash
dotnet ef migrations add CreateOrdersTable --project Apps/Shop/Data/Data.EntityFramework
```

### SyncAllProjects.cs

```bash
dotnet run tools/SyncAllProjects.cs
```

Rebuilds `{Prefix}.All.slnx` from every `.csproj` on disk, grouped by the first two path segments. Idempotent:
an already-current solution is left byte-identical, so it produces no diff. Run it after a rebase leaves the
solution file conflicted — regenerating beats resolving.

### ValidateConventions.cs

```bash
dotnet run tools/ValidateConventions.cs -- --path .
```

Exits non-zero on a violation, so it works as a CI step. Four rules, each for something that produces *working*
code that fails later:

| Rule | What goes wrong without it |
|---|---|
| No direct `DateTime.Now` / `UtcNow` | The behaviour becomes untestable — you cannot test a thirty-day rule without waiting thirty days. Inject `IClock`. |
| Events carry only primitives | An event is deserialized by code built from a different commit. A property typed as an entity ties the wire format to that entity's shape. |
| No `IOptions` in a use case | The use case cannot be constructed in a test without building a configuration, and its real dependencies hide inside a settings bag. |
| Non-nullable DAL columns carry `[Required]` | Without it EF infers a nullable column, then throws on read when a row legitimately holds null. |
| DAL columns don't use the `required` keyword | It forces assignment in the object initializer, which the `From()` then `Apply()` construction cannot do. The primary key is exempt — that one *is* set in the initializer. |

Rules deliberately under-report. A false positive that has to be argued with is worse than a miss.

Genuine exceptions are declared in the code, not special-cased in the tool:

```csharp
public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow; // conventions: allow — an event records when it happened, and has no clock to inject
```

The reason is required — a bare `// conventions: allow` does not suppress anything, so an exemption has to
argue for itself in review.

## Not yet ported

These exist in MagicDoor's backend and have not been brought across: `CreateApp`, `CreateAppLib`, `CreateLib`
(project scaffolds), `AddEvent`, `AddLib`, and `GenerateAssemblyCatalog`. Until `CreateApp` lands, a new
service's project skeleton is created by hand; `AddEntity` and the linter work against it either way.
