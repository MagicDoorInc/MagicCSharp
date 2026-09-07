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
dotnet tool install -g MagicCSharp.Cli
```

Then, in any directory:

```bash
mcs init --prefix Acme
mcs create-app --name Shop --database shop
dotnet run --project Apps/Shop/Shop.App
```

| | |
|---|---|
| `mcs init --prefix Acme` | set this directory up as a repository |
| `mcs create-app --name Shop --database shop` | a service |
| `mcs create-domain -s Shop -n Orders --models --tests` | a domain |
| `mcs create-domain -s Shop -n Orders.App --tests` | that domain's endpoints |
| `mcs add-entity -s Shop -d Orders -n Order --paginated` | an entity and its repository |
| `mcs create-app-lib -s Shop -n Processors --tests` | a library inside one service |
| `mcs create-lib --name Events --tests` | a shared library |
| `mcs sync` | rebuild the all-projects solution |
| `mcs validate` | lint the conventions the compiler cannot |
| `mcs templates list \| where \| eject` | see and override the generators' templates |

Update with `dotnet tool update -g MagicCSharp.Cli`.

### Pin it for a team

A global install means everyone updates on their own schedule, which is fine alone and a nuisance in a team —
one person's scaffolding drifts from another's. A tool manifest pins the version in the repository:

```bash
dotnet new tool-manifest
dotnet tool install MagicCSharp.Cli
# commit .config/dotnet-tools.json
```

Teammates then run `dotnet tool restore` once and use `dotnet mcs ...`. Upgrading is a commit everyone
picks up, rather than a message in chat.

`dotnet new magiccsharp-repo` sets this up for you:

```bash
dotnet new install MagicCSharp.Templates
dotnet new magiccsharp-repo -n Acme
cd Acme && dotnet tool restore
```

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

Either route gives you the same six things — and `mcs init` will not overwrite any that already exist:

| File | Why |
|---|---|
| `magiccsharp.json` | Holds the prefix. Its presence is how every other tool knows it is at the repository root. |
| `Directory.Build.props` | Target framework, nullable, implicit usings, warnings-as-errors — inherited by every project, so a `.csproj` carries only what is specific to it. |
| `Directory.Packages.props` | Central package management: one version per package for the whole repository, so two projects cannot disagree. |
| `{Prefix}.All.slnx` | Every project. Generated — see `mcs sync`. |
| `Apps/`, `Libs/` | The two top-level directories, with `.gitkeep` so they survive a fresh clone. |

Then create your first service:

```bash
mcs create-app --name Shop --database shop
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
    Shop.App/                    host: Program.cs, configuration, references
    Shop.Domains/
      Orders/
        Default/                 use cases, event handlers
        Models/                  entities, edits, filters
        Tests/
        App/                     this domain's endpoints: controllers, DTOs
        Fulfilment/              a subdomain: the same shape, one level down
    Shop.Processors/             an app library: one service, but not a domain
    Data/
      Data.Models/               repository interfaces — for every domain
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
mcs create-lib --name Events --tests
```

Creates `Libs/Events/Default/Acme.Libraries.Events.csproj`, and a Tests project beside it. Dots in the name
nest directories:

```bash
mcs create-lib --name Clients.Billing
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

## Making the templates your own

Everything the generators write comes from a `.hbs` template, and a team can replace any one of them without
forking the rest.

```bash
mcs templates list              # every template, and which layer provides it
mcs templates eject Entities/dal.cs.hbs
```

Two layers, first match winning: `.magiccsharp/templates/` in your repository, then the built-ins embedded in
`mcs`. Resolution is per file, so overriding the DAL template leaves the other nineteen built-in and still
tracking upstream. Reverting is deleting your copy.

**Once you have more than one repository, put the templates in a repository of their own** and add it as a
submodule at `.magiccsharp/templates` — otherwise house style drifts between repositories and a fix to one
never reaches the others.

**[How to override templates, and how to build a shared template repository →](template-overrides.md)**

---

## Inside a service

### Where code goes

A service holds exactly one host project (`Shop.App` — `Program.cs`, configuration, and references), the two
data projects, and then any number of **libraries**. A library is three optional projects in one directory:
`Default/` (the code), `Models/` (the types other projects may depend on) and `Tests/`. Every library has
that same shape. What differs is what it is for, and that is decided by where it sits and what it is called.

There are four places code can go, and the question that picks between them is **who uses it**:

| You are writing | It goes in | Command |
|---|---|---|
| Code two or more **services** use: an event contract, a typed client | a shared library, `Libs/Events/` | `mcs create-lib` |
| A business area with its own entities, rules, use cases and event handlers | a domain, `Apps/Shop/Shop.Domains/Orders/` | `mcs create-domain` |
| That domain's endpoints: controllers, request and response types | the domain's App, `.../Orders/App/` | `mcs create-domain --name Orders.App` |
| Code one service uses from several domains, that is not itself a domain: a payment-processor adapter, test fixtures | an app library, `Apps/Shop/Shop.Processors/` | `mcs create-app-lib` |

Two sentences carry most of it:

**A domain is an app library with a job.** Technically it is the same three projects. What makes it a domain
is that it sits under `Shop.Domains/`, owns entities and rules, and the host references it.  An app library
sits elsewhere in the service and the host does *not* reference it — something in a domain does.

**A domain's `App` is its face, not a second domain.** `Orders/App` references `Orders`, never the reverse.
Anything that needs `HttpContext` — the current user, a header, a status code — belongs in `Orders/App`.
Anything that would still be true if the service spoke no HTTP at all belongs in `Orders/Default`.

Why this is worth the structure: the host project becomes a list of references and a `Program.cs`, and each
domain is one directory holding its rules, its types, its endpoints and its tests, readable top to bottom by
someone who has never seen the rest of the service. Five domains' controllers do not pile into one
`Controllers/` folder. Moving a domain to another service is moving a directory and changing two references.

**Names nest.** Dots in `--name` become directories, and the leaf gets the three projects:

| `--name` | Directory | Assembly |
|---|---|---|
| `Processors` | `Apps/Shop/Shop.Processors/` | `Acme.Shop.Processors` |
| `Domains.Orders` | `Apps/Shop/Shop.Domains/Orders/` | `Acme.Shop.Domains.Orders` |
| `Domains.Orders.App` | `Apps/Shop/Shop.Domains/Orders/App/` | `Acme.Shop.Domains.Orders.App` |
| `Domains.Orders.Fulfilment` | `Apps/Shop/Shop.Domains/Orders/Fulfilment/` | `Acme.Shop.Domains.Orders.Fulfilment` |

The first segment is glued to the service name; every one after it is a subdirectory.

### A domain

```bash
mcs create-domain --solution Shop --name Orders --models --tests
```

`--solution` takes the service name. Leave it out entirely when the repository has only one service; with
several and no flag, the command lists them rather than guessing. `create-domain --name Orders` is exactly
`create-app-lib --name Domains.Orders` — use whichever reads better.

Up to three projects under `Apps/Shop/Shop.Domains/Orders/`:

- **`Default/`** — use cases and event handlers. The logic.
- **`Models/`** — entities, edits, filters. Separate so the data projects can reference the entities without
  reaching the logic. `add-entity` needs this, so pass `--models` unless you have a reason not to.
- **`Tests/`** — comes with `MagicCSharp.Testing` referenced.

`Default` gets a reference to `Models` automatically, and the service's `App` project gets a reference to
`Default`. That second one is not cosmetic: without it the domain's assembly is not deployed with the
service, so its use cases are never registered and its event handlers never run.

Name domains in the plural and entities in the singular — `Orders` and `Order` — so the generated
`IOrdersRepository` reads correctly.

### The domain's endpoints

```bash
mcs create-domain --solution Shop --name Orders.App --tests
```

`Apps/Shop/Shop.Domains/Orders/App/`, holding the controllers for this domain and the request and response
types they use. It is created with the ASP.NET shared framework referenced and a reference back to
`Orders/Default`, so its controllers can call the domain's use cases.

The test for what belongs here: **would this code exist if the service had no HTTP?** If yes, it belongs in
`Orders/Default`.

There is nothing else to wire. The host references this project, and that is enough for both halves:
controllers are found because ASP.NET reads the referenced assemblies that use MVC, and use cases and event
handlers are found because MagicCSharp loads every assembly deployed beside the executable before it scans.
If you are coming from a codebase that touches a type from each assembly at startup to force it to load, you
do not need that here.

Create the `.App` before its domain exists and the command says so and adds no reference; create the domain
and run it again, and the second run adds it.

### Splitting a domain

```bash
mcs create-domain --solution Shop --name Orders.Fulfilment --models --tests
mcs add-entity    --solution Shop --domain Orders.Fulfilment --name Shipment
```

A subdomain, when one domain has grown enough to divide. It gets the same three projects one level down, and
the host references it like any other domain.

A subdomain may depend on its parent, never the reverse. That reference is *not* added for you — most
subdomains want it, but a project reference nothing needs never announces itself — so the command prints the
line to run if you want it.

One level of nesting is normal. Three is usually a sign the domain boundary is in the wrong place rather
than that the tool should go deeper.

### An app library

```bash
mcs create-app-lib --solution Shop --name Processors --tests
```

`Apps/Shop/Shop.Processors/` — one service, but not a domain: it owns no entities and no business rules, it
supports the domains that do. The host does not reference it; a domain should. The command prints the line.

**Test fixtures** are an ordinary app library. Create one, then add `MagicCSharp.Testing` and `xunit` to its
`Default` project and reference it from the `Tests` projects that share those fixtures:

```bash
mcs create-app-lib --solution Shop --name Testing.Fixtures
dotnet add Apps/Shop/Shop.Testing/Fixtures/Default package MagicCSharp.Testing
```

### What the tool wires, and what it leaves you

Three things follow from the name alone:

| When | The tool |
|---|---|
| the name starts with `Domains.` | references it from the host, so it is deployed and discovered |
| the name ends with `.App` | gives it the ASP.NET framework reference and points it at its domain |
| you passed `--models` | points `Default` at `Models` |

Everything else is yours: a subdomain's reference to its parent, and any reference to an app library.

**References point down the tree and toward the domain** — host to domain, `.App` to domain, subdomain to
parent, `Default` to `Models` — and never back. The day something makes `Orders/Default` reference
`Orders/App` to reuse a type, the build fails with a cycle; the fix is to move that type into `Models`, not
to add the reference.

### An entity

```bash
mcs add-entity --solution Shop --domain Orders --name Order --paginated
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

| Command | Does |
|---|---|
| `mcs init` | Sets up the repository. Run once. |
| `mcs create-app` | New service: host project, solution, and the data projects unless `--no-database`. |
| `mcs create-domain` | New domain inside a service, or its endpoints, or a subdomain. |
| `mcs create-app-lib` | New library inside one service that is not a domain. |
| `mcs create-lib` | New shared library under `Libs/`. |
| `mcs add-entity` | Entity across its four files, registered. |
| `mcs sync` | Rebuilds `{Prefix}.All.slnx` from disk. |
| `mcs validate` | Lints the conventions the compiler cannot. Exits non-zero — use it in CI. |
| `mcs templates` | See and override the generators' templates. |

Every one takes `--help`, and the [CLI reference](../src/MagicCSharp.Cli/README.md) lists the options.

### Regenerating the wide solution

```bash
mcs sync
```

`create-app`, `create-domain`, `create-app-lib` and `create-lib` run this themselves, so you rarely call it. The two times you do:
after a merge or rebase leaves `{Prefix}.All.slnx` conflicted — take either side, or delete the file, and
regenerate rather than resolving by hand — and after moving or deleting a project outside the tools.

Two properties they all share, which is what makes them safe to run against a repository you have been
working in:

**Nothing is overwritten.** An existing file is reported and skipped. Regenerating over something you have
edited would throw the edits away, so it never happens — if you want a file regenerated, delete it first.

**Re-running changes nothing.** Registrations are not duplicated, solutions are rewritten only when the
content actually differs. Run any of them twice and the second run produces no diff.

`create-app`, `create-domain`, `create-app-lib` and `create-lib` run `sync` for you, so a new project is in the wide solution
without a second command.

### Conventions

```bash
mcs validate --path .
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

`mcs init` skips what already exists, so it composes with what you have. In an existing repository, expect to
do three things by hand:

1. **Reconcile `Directory.Build.props`.** If you already have one, `mcs init` leaves it alone — check it sets
   a target framework and `Nullable`.
2. **Adopt central package management, or don't.** `Directory.Packages.props` only takes effect for projects
   that omit versions from their `PackageReference` entries. Existing projects with inline versions keep
   working until you move them.
3. **Move projects into `Apps/` and `Libs/`.** The tools only generate into those paths; they will not
   relocate what you have. `mcs sync` picks up whatever it finds, so you can move projects gradually.

You can also take part of it. The commands want the full layout, but `mcs validate` works on any
directory, and the packages themselves want nothing.

---

## Version pinning

`mcs init` writes the MagicCSharp version into `Directory.Packages.props`. It defaults to the version the
tools shipped with; override it with `--package-version`. The release script keeps that default in step with
what is actually published, so a freshly scaffolded repository never points at a version that predates its
own tooling.
