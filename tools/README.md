# Tools

Scaffolding and linting for a repository built on MagicCSharp. Each tool is a single-file .NET program — no
project, no build step, dependencies declared inline with `#:package`. Requires the **.NET 10 SDK**.

**The structure these create is optional.** The MagicCSharp packages work in any project layout. See
[docs/repository-layout.md](../docs/repository-layout.md) for what the structure is, why each boundary is
where it is, and how to adopt part or none of it.

## Getting started

Copy this directory into the root of your repository, then:

```bash
dotnet run tools/InitRepo.cs -- --prefix Acme
dotnet run tools/CreateApp.cs -- --name Shop --database shop
dotnet run --project Apps/Shop/Shop.App
```

Every tool takes `--help`, which prints its current options — trust that over this file if they disagree.

---

## InitRepo — set the repository up

```bash
dotnet run tools/InitRepo.cs -- --prefix Acme
dotnet run tools/InitRepo.cs -- --prefix Acme --package-version 0.1.0
```

| Option | |
|---|---|
| `-p, --prefix` | Namespace and solution-name root. `Acme` gives `Acme.Shop.slnx`. Required. |
| `--package-version` | MagicCSharp version pinned in `Directory.Packages.props`. Defaults to the version these tools shipped with. |

Run once, at the repository root. Writes `magiccsharp.json`, `Directory.Build.props`,
`Directory.Packages.props`, `{Prefix}.All.slnx`, `Apps/` and `Libs/`. Skips anything that already exists, so
it is safe in a repository that already has a README and a `.gitignore`.

The prefix is baked into every namespace, so it is awkward to change later. Pick your company or product name.

## CreateApp — a service

```bash
dotnet run tools/CreateApp.cs -- --name Shop --database shop
dotnet run tools/CreateApp.cs -- --name Notifications --no-database
dotnet run tools/CreateApp.cs -- --name Shop --database shop --port 5300
```

| Option | |
|---|---|
| `-n, --name` | Service name, one PascalCase word. Required. |
| `-d, --database` | Database name, lowercase with underscores. Creates the data projects. |
| `--no-database` | For a service that owns no tables. One of these two is required. |
| `-p, --port` | Local port. Defaults to one no other service's `launchSettings.json` claims. |

Creates `Apps/{Name}/{Name}.App/` and `{Prefix}.{Name}.slnx`, plus `Data/Data.Models/` and
`Data/Data.EntityFramework/` unless `--no-database`. The service builds and answers on `/hello` immediately.

## CreateAppLib — a domain inside a service

```bash
dotnet run tools/CreateAppLib.cs -- --solution Acme.Shop.slnx --name Domains.Orders --models --tests
dotnet run tools/CreateAppLib.cs -- --solution Acme.Shop.slnx --name Domains.Orders.App --tests
```

| Option | |
|---|---|
| `-s, --solution` | The service's solution file. Required. |
| `-n, --name` | Library name, e.g. `Domains.Orders`. Required. |
| `-m, --models` | Also create `Models/`. **`AddEntity` requires this**, so pass it unless you have a reason not to. |
| `-t, --tests` | Also create `Tests/`, with `MagicCSharp.Testing` referenced. |

`Default/` holds use cases and event handlers, `Models/` holds entities. `Default` is wired to `Models`
automatically; never add the reverse, since `Models` is what the data projects depend on.

## CreateLib — a shared library

```bash
dotnet run tools/CreateLib.cs -- --name Events --tests
dotnet run tools/CreateLib.cs -- --name Clients.Billing
```

| Option | |
|---|---|
| `-n, --name` | Library name. Dots nest directories: `Clients.Billing` → `Libs/Clients/Billing/`. Required. |
| `-t, --tests` | Also create a Tests project. |

For code **more than one service uses** — events, service clients, real infrastructure. Consume it with an
ordinary reference:

```bash
dotnet add Apps/Shop/Shop.App reference Libs/Events/Default/Acme.Libraries.Events.csproj
```

The guide has the longer argument about what belongs here and what does not.

## AddEntity — an entity and its repository

```bash
dotnet run tools/AddEntity.cs -- --solution Acme.Shop.slnx --domain Orders --name Order --paginated
dotnet run tools/AddEntity.cs -- --solution Acme.Shop.slnx --domain Access --name ApiKey --use-key
```

| Option | |
|---|---|
| `-s, --solution` | The service's solution file. Required. |
| `-d, --domain` | Owning domain. Its `Models` project must exist. Required. |
| `-n, --name` | Entity name, PascalCase singular. Required. |
| `-p, --paginated` | Also give the repository page-at-a-time reads. |
| `-k, --use-key` | Key by unguessable string instead of Snowflake id. |

Writes the entity/edit/filter, the repository interface, the DAL and the EF repository, adds the `DbSet` and
registers the repository — imports included. The generated `ToEntity`, `Apply` and `ApplyFilter` carry
`TODO`s, because only you know the columns. Then:

```bash
dotnet ef migrations add CreateOrdersTable --project Apps/Shop/Data/Data.EntityFramework
```

**`--use-key` when the key is public.** A Snowflake id encodes the time it was issued and sits next to its
neighbours, so putting one in a URL leaks both when the record was created and roughly how many exist. Use it
for invite links, webhook targets and API keys.

## Templates — see and override the generators' templates

```bash
dotnet run tools/Templates.cs -- list
dotnet run tools/Templates.cs -- list --overridden
dotnet run tools/Templates.cs -- where
dotnet run tools/Templates.cs -- eject Entities/dal.cs.hbs [--force]
```

Templates resolve through three layers, first match winning: `.magiccsharp/templates/` in the repository,
then the installed `~/.magiccsharp/templates/`, then a vendored `tools/Templates/`.

`eject` copies a built-in template into the repository so you can change it. Resolution is per file, so
overriding one template leaves the rest built-in and still tracking upstream. Delete your copy to revert.

The override directory comes from `"templates"` in `magiccsharp.json`, defaulting to
`.magiccsharp/templates`; set it to `""` to disable overrides.

## SyncAllProjects — rebuild the wide solution

```bash
dotnet run tools/SyncAllProjects.cs
```

No options. Rebuilds `{Prefix}.All.slnx` from every `.csproj` on disk, grouped by the first two path
segments.

`CreateApp`, `CreateAppLib` and `CreateLib` run it themselves, so you rarely call it directly. The two times
you do:

- **After a merge or rebase conflicts in the solution file.** Take either side, or delete it, and regenerate —
  faster and more reliable than resolving it by hand.
- **After moving or deleting a project** without going through the tools.

Idempotent, and byte-identical output when nothing changed, so it produces no diff on a no-op run.

## ValidateConventions — lint what the compiler cannot

```bash
dotnet run tools/ValidateConventions.cs -- --path .
dotnet run tools/ValidateConventions.cs -- --path Apps/Shop
```

| Option | |
|---|---|
| `--path` | Directory to scan. Defaults to the working directory. |

Exits non-zero when it finds anything, so it works as a CI step. Four rules, each for something that compiles
and fails later — see the guide for the reasoning behind each.

Real exceptions are declared in the code, with a reason:

```csharp
public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow; // conventions: allow — an event records when it happened, and has no clock to inject
```

A bare `// conventions: allow` suppresses nothing.

---

## Two guarantees

**Nothing is overwritten.** An existing file is reported and skipped, because regenerating over something you
edited would throw the edits away. Delete a file if you want it regenerated.

**Re-running changes nothing.** Registrations are not duplicated; solutions are rewritten only when the
content differs. Run any tool twice and the second run produces no diff.

## Layout of this directory

```
tools/
  Directory.Build.props       pins the scripts to net10.0 — see below
  *.cs                        the tools
  Templates/                  built-in; a repository overrides individual files
    Repo/                     Directory.Build.props, Directory.Packages.props
    Apps/                     service host, appsettings, data projects
    Libraries/                library and test csproj
    Entities/                 entity, repository interface, DAL, EF repository
```

`Directory.Build.props` here deliberately does **not** inherit the repository's. The scripts are compiled and
run by the SDK on whichever machine is scaffolding, so a repository targeting net9.0 would otherwise produce
scripts the .NET 10 runtime refuses to launch. Keep the file with the scripts when copying `tools/` across.

The templates are ordinary text — edit them and every future generated file follows suit.

## Not ported from MagicDoor's backend

`AddEvent` (event scaffold), `AddLib` (add a shared library to a service solution, resolving transitive
references) and `GenerateAssemblyCatalog`. The last matters once a service spans many projects: .NET loads an
assembly only when one of its types is first touched, so use cases in a project the host never references are
not found by registration. Until it is ported, touch one type per project during startup.
