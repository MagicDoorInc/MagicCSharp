# The repository layout

**This is optional.** The MagicCSharp packages work in any project structure — a single console app, a
vertical-slice API, whatever you already have. Nothing in the libraries reads `magiccsharp.json` or cares
where a file sits.

What this document describes is the structure MagicDoor runs its backend on: a handful of services in one
repository, each with its own solution, sharing a set of libraries. If that shape matches where you are
heading, the tools in `tools/` create and maintain it for you. If it does not, ignore all of it and use the
packages directly.

---

## Setting it up

You need the **.NET 10 SDK**. The scripts are single-file programs that declare their own dependencies, so
there is nothing else to install.

### Install the CLI

```bash
curl -fsSL https://raw.githubusercontent.com/MagicDoorInc/MagicCSharp/master/install.sh | bash
```

Puts the scripts in `~/.magiccsharp/tools` and a dispatcher named `mcs` on your PATH. Then, in any
directory:

```bash
mcs init --prefix Acme
mcs create-app --name Shop --database shop
dotnet run --project Apps/Shop/Shop.App
```

| | |
|---|---|
| `mcs init --prefix Acme` | set this directory up as a repository |
| `mcs create-app --name Shop --database shop` | a service |
| `mcs create-domain -s Acme.Shop.slnx -n Domains.Orders --models --tests` | a domain |
| `mcs add-entity -s Acme.Shop.slnx -d Orders -n Order --paginated` | an entity and its repository |
| `mcs create-lib --name Events --tests` | a shared library |
| `mcs sync` | rebuild the all-projects solution |
| `mcs validate` | lint the conventions the compiler cannot |
| `mcs update` | upgrade the tools |

`mcs` checks for a newer build once a day, in the background, and prints a one-line notice on the next
command. It never blocks what you asked for, only says anything on a terminal, and
`MAGICCSHARP_NO_UPDATE_CHECK=1` turns it off.

Re-running the installer upgrades in place. `MAGICCSHARP_HOME` moves the install, `MAGICCSHARP_REF` pins a
branch or tag, `NO_MODIFY_PATH=1` leaves your shell profile alone.

### Without installing anything

If you would rather not install a CLI, the same scaffolding ships as a `dotnet new` template:

```bash
dotnet new install MagicCSharp.Templates
dotnet new magiccsharp-repo -n Acme
cd Acme
```

| Option | Default | |
|---|---|---|
| `-n, --name` | — | Namespace and solution-name root |
| `--MagicCSharpVersion` | the version the template shipped with | MagicCSharp packages to pin |
| `--TargetFramework` | `net10.0` | `net10.0` or `net9.0` |

That gives you everything below plus a `tools/` directory inside the repository — useful when you want the
scripts committed alongside the code so a teammate cloning it needs nothing installed. The scripts' own
`--help` examples are rewritten to your prefix.

### An existing repository

`mcs init` works there too — it writes only what is missing, so it composes with a repository that already has a README, a
`.gitignore` and projects of its own:

```bash
cd my-existing-repo
mcs init --prefix Acme
```

See [Adding this to an existing repository](#adding-this-to-an-existing-repository).

The prefix is your namespace and solution-name root. `Acme` gives you `Acme.Shop.slnx`, assemblies named
`Acme.Shop.App`, namespaces like `Acme.Shop.Domains.Orders`. Pick your company or product name; it is
awkward to change later because it is baked into every namespace.

Either route gives you the same six things — and `InitRepo` will not overwrite any that already exist:

| File | Why |
|---|---|
| `magiccsharp.json` | Holds the prefix. Its presence is how every other tool knows it is at the repository root. |
| `Directory.Build.props` | Target framework, nullable, implicit usings, warnings-as-errors — inherited by every project, so a `.csproj` carries only what is specific to it. |
| `Directory.Packages.props` | Central package management: one version per package for the whole repository, so two projects cannot disagree. |
| `{Prefix}.All.slnx` | Every project. Generated — see `SyncAllProjects`. |
| `Apps/`, `Libs/` | The two top-level directories, with `.gitkeep` so they survive a fresh clone. |

Then create your first service:

```bash
dotnet run tools/CreateApp.cs -- --name Shop --database shop
```

You now have a service that builds and runs:

```bash
dotnet run --project Apps/Shop/Shop.App
curl http://localhost:5200/hello
```

---

## The shape

```
magiccsharp.json
Directory.Build.props            settings every project inherits
Directory.Packages.props         one version per package
Acme.All.slnx                    every project — generated
Acme.Shop.slnx                   one service
Acme.Notifications.slnx

Apps/
  Shop/
    Shop.App/                    host: Program.cs, controllers
    Shop.Domains/
      Orders/
        Default/                 use cases, event handlers
        Models/                  entities, edits, filters
        Tests/
    Data/
      Data.Models/               repository interfaces
      Data.EntityFramework/      DALs, EF repositories, context, migrations

Libs/
  Events/Default/                shared across services
  Clients/Billing/Default/
```

### Two solutions, on purpose

Day to day you open `Acme.Shop.slnx` and build one service — seconds, not minutes, and the projects in front
of you are the ones you are working on. `Acme.All.slnx` exists for the times you need to see everything:
renaming something in a shared library, or checking what a change breaks. It is generated from disk, so it is
never out of date and never worth resolving a merge conflict in.

### Why entities live in the domain, not in Data

`Order` is in `Shop.Domains/Orders/Models`. `OrderDal` and `OrdersEfRepository` are in `Data/`.

The domain does not depend on how it is stored. `Data.Models` — which holds only repository interfaces —
references the domain's Models project, and `Data.EntityFramework` implements those interfaces. The arrow
points from storage toward the domain, never back. That is what stops a repository calling a use case, and
what lets you read a domain without reading a single EF attribute.

`Data.Models` deliberately does not reference Entity Framework at all. It gets `MagicCSharp.Data`, which is
contracts only.

---

## `Libs/` — shared libraries

`Libs/` is for code **more than one service uses**. That is the whole rule.

```bash
dotnet run tools/CreateLib.cs -- --name Events --tests
```

Creates `Libs/Events/Default/Acme.Libraries.Events.csproj`, and a Tests project beside it. Dots in the name
nest directories:

```bash
dotnet run tools/CreateLib.cs -- --name Clients.Billing
# → Libs/Clients/Billing/Default/Acme.Libraries.Clients.Billing.csproj
```

Worth doing once you have a few — a dozen client libraries sitting flat next to a dozen unrelated ones stops
being navigable quickly.

Consume one with an ordinary project reference:

```bash
dotnet add Apps/Shop/Shop.App reference Libs/Events/Default/Acme.Libraries.Events.csproj
```

### What belongs here

**Events.** The strongest case. If Shop publishes `OrderPlaced` and Notifications handles it, both need the
type, and it cannot live in either. A shared events library is the contract between them.

**Service clients.** The typed HTTP client for calling Shop belongs next to Shop's events, not copy-pasted
into every caller.

**Genuine infrastructure.** A PDF writer, a rate limiter, an S3 wrapper — things with no domain knowledge at
all.

### What does not

**Anything one service uses.** It belongs in that service's domain. Moving it to `Libs/` "because it might be
shared later" is how the directory turns into a junk drawer, and you can always move it when the second
caller actually appears.

**Domain logic.** A shared library that knows what a lease is has quietly become a second home for the leasing
domain. Now two places define the rules and they will disagree.

**Entities.** Those are domain-owned, in the service that owns the table.

The test: *would a second service genuinely use this today?* Not "could", not "might" — does one.

### The cost

Every shared library couples the services that reference it. Change it and you rebuild and redeploy all of
them, and a mistake breaks all of them at once. That is a fair trade for an event contract, which has to be
shared for the system to work. It is a bad trade for a helper that saved you twenty lines.

When a shared library keeps changing for reasons that only concern one service, it was never shared code — it
was that service's code in the wrong place.

---

## Adding to a service

### A domain

```bash
dotnet run tools/CreateAppLib.cs -- --solution Acme.Shop.slnx --name Domains.Orders --models --tests
```

Up to three projects under `Apps/Shop/Shop.Domains/Orders/`:

- **`Default/`** — use cases and event handlers. The logic.
- **`Models/`** — entities, edits, filters. Separate so the data projects can reference the entities without
  reaching the logic. `AddEntity` needs this, so pass `--models` unless you have a reason not to.
- **`Tests/`** — comes with `MagicCSharp.Testing` referenced.

`Default` gets a reference to `Models` automatically. Never add the reverse — `Models` is what the data
projects depend on, and a cycle follows immediately.

### An entity

```bash
dotnet run tools/AddEntity.cs -- --solution Acme.Shop.slnx --domain Orders --name Order --paginated
```

Four files across three projects, all of which have to agree about names, namespaces and generic arguments:

| File | Project |
|---|---|
| `Order.cs` — entity, edit, filter | `Shop.Domains/Orders/Models` |
| `IOrdersRepository.cs` | `Data/Data.Models` |
| `OrderDal.cs` | `Data/Data.EntityFramework` |
| `OrdersEfRepository.cs` | `Data/Data.EntityFramework` |

It also adds the `DbSet` to the context and registers the repository, imports included.

The generated `ToEntity`, `Apply` and `ApplyFilter` have `TODO`s in them. That is deliberate — only you know
the columns. Fill them in, then:

```bash
dotnet ef migrations add CreateOrdersTable --project Apps/Shop/Data/Data.EntityFramework
```

**`--use-key` when the key is public.** A Snowflake id encodes the time it was issued and sits next to its
neighbours, so putting one in a URL leaks both when the record was created and roughly how many exist. A
random string key leaks neither. Use it for invite links, webhook targets and API keys.

**`--paginated` when the result set grows without bound.** Leave it off for a small lookup table, so the
interface says so.

---

## The tools

| Tool | Does |
|---|---|
| `InitRepo` | Sets up the repository. Run once. |
| `CreateApp` | New service: host project, solution, and the data projects unless `--no-database`. |
| `CreateAppLib` | New domain inside a service. |
| `CreateLib` | New shared library under `Libs/`. |
| `AddEntity` | Entity across its four files, registered. |
| `SyncAllProjects` | Rebuilds `{Prefix}.All.slnx` from disk. |
| `ValidateConventions` | Lints the conventions the compiler cannot. Exits non-zero — use it in CI. |

Every one takes `--help`, and [tools/README.md](../tools/README.md) has the options and a worked example for
each.

### Regenerating the wide solution

```bash
dotnet run tools/SyncAllProjects.cs
```

`CreateApp`, `CreateAppLib` and `CreateLib` run this themselves, so you rarely call it. The two times you do:
after a merge or rebase leaves `{Prefix}.All.slnx` conflicted — take either side, or delete the file, and
regenerate rather than resolving by hand — and after moving or deleting a project outside the tools.

Two properties they all share, which is what makes them safe to run against a repository you have been
working in:

**Nothing is overwritten.** An existing file is reported and skipped. Regenerating over something you have
edited would throw the edits away, so it never happens — if you want a file regenerated, delete it first.

**Re-running changes nothing.** Registrations are not duplicated, solutions are rewritten only when the
content actually differs. Run any of them twice and the second run produces no diff.

`CreateApp`, `CreateAppLib` and `CreateLib` run `SyncAllProjects` for you, so a new project is in the wide
solution without a second command.

### Conventions

```bash
dotnet run tools/ValidateConventions.cs -- --path .
```

Four rules, each for something that produces code which *compiles and then fails later*:

| Rule | What goes wrong |
|---|---|
| No direct `DateTime.Now` / `UtcNow` | The behaviour becomes untestable — you cannot test a thirty-day rule without waiting thirty days. Inject `IClock`. |
| Events carry only primitives | An event is deserialized by code built from a different commit. A property typed as an entity ties the wire format to that entity's shape. |
| No `IOptions` in a use case | It cannot then be constructed in a test without building a configuration, and its real dependencies hide inside a settings bag. |
| Non-nullable DAL columns carry `[Required]`, and don't use the `required` keyword | Without `[Required]` EF infers a nullable column. The `required` keyword forces assignment in the object initializer, which the `From()`/`Apply()` construction cannot do — the primary key is the exception, and is allowed. |

Rules deliberately under-report: a false positive you have to argue with is worse than a miss. Real
exceptions are declared in the code, with a reason:

```csharp
public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow; // conventions: allow — an event records when it happened, and has no clock to inject
```

A bare `// conventions: allow` suppresses nothing. The reason is required so the exemption argues for itself
in review.

---

## Adding this to an existing repository

`InitRepo` skips what already exists, so it composes with what you have. In an existing repository, expect to
do three things by hand:

1. **Reconcile `Directory.Build.props`.** If you already have one, `InitRepo` leaves it alone — check it sets
   a target framework and `Nullable`.
2. **Adopt central package management, or don't.** `Directory.Packages.props` only takes effect for projects
   that omit versions from their `PackageReference` entries. Existing projects with inline versions keep
   working until you move them.
3. **Move projects into `Apps/` and `Libs/`.** The tools only generate into those paths; they will not
   relocate what you have. `SyncAllProjects` picks up whatever it finds, so you can move projects gradually.

You can also take part of it. The scripts want the full layout, but `ValidateConventions` works on any
directory, and the packages themselves want nothing.

---

## Version pinning

`InitRepo` writes the MagicCSharp version into `Directory.Packages.props`. It defaults to the version the
tools shipped with; override it with `--package-version`. The release script keeps that default in step with
what is actually published, so a freshly scaffolded repository never points at a version that predates its
own tooling.
