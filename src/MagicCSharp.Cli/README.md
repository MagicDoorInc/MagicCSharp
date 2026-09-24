# MagicCSharp.Cli

`mcs` — scaffolding for a repository built on MagicCSharp. A service, a domain, an entity across the four
files that have to agree about it, and a validator that fails CI when the tree rots or someone calls
`DateTime.Now`. The layout it creates is how a service stays readable at a hundred use cases: a tree of
domains, each owning its use cases, entities, endpoints and tests.

```bash
dotnet tool install -g MagicCSharp.Cli

mcs init --prefix Acme
mcs create-app --name Shop --database shop
dotnet run --project Apps/Shop/Shop.App
```

Or pin it per repository, so the whole team gets the same version:

```bash
dotnet new tool-manifest
dotnet tool install MagicCSharp.Cli
# commit .config/dotnet-tools.json; teammates run: dotnet tool restore
```

| Command | |
|---|---|
| `mcs init --prefix Acme` | set the current directory up as a repository |
| `mcs create-app --name Shop --database shop` | an app: a deployable service |
| `mcs create-domain -s Shop -n Orders --models --tests` | a domain inside that app |
| `mcs create-domain -s Shop -n Orders.App --tests` | that domain's endpoints |
| `mcs create-app-lib -s Shop -n Processors --tests` | a library inside one service |
| `mcs create-lib --name Events --tests` | a shared library |
| `mcs add-entity -s Shop -d Orders -n Order --paginated` | an entity and its repository |
| `mcs sync` | rebuild the all-projects solution |
| `mcs validate` | lint the conventions the compiler cannot |
| `mcs references --project <csproj>` | every project a project depends on, directly or not |
| `mcs affected --base <commit> --json` | the apps a range of commits changed, for CI to build and deploy |
| `mcs update ai-files` | refresh `AGENTS.md`, `CLAUDE.md` and the `.ai-knowledge/` guides to this version |
| `mcs templates list \| where \| eject` | see and override the generators' templates |

**Nothing is overwritten** — an existing file is reported and skipped. **Re-running changes nothing**, so
every command is safe to repeat.

`add-entity` writes the entity, its edit and filter, the repository interface, the DAL and the EF
repository, adds the `DbSet` and registers the repository — across three projects that each have to agree
about names, namespaces and generic arguments. Not typing saved so much as a class of mistake removed.

`validate` catches the things that compile and then fail later: `DateTime.Now` where `TimeProvider` belongs, an
entity on an event, `IOptions` in a use case, a non-nullable column without `[Required]`. It exits non-zero,
so put it in CI.

`affected` is what lets CI build, test and deploy only the apps a change touched. It follows each app's project
references, so a change to a shared library under `Libs/` picks every app that uses it and no other; a change
to `Directory.Packages.props` or another file every build reads picks them all. `--json` prints a GitHub
Actions matrix. The [CI/CD guide](https://github.com/MagicDoorInc/MagicCSharp/blob/master/docs/ci-cd.md) sets
up the whole pipeline around it.

`init` also makes the code-style conventions compile errors: the `Directory.Build.props` it writes references
[MagicCSharp.Analyzers](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Analyzers/README.md)
for every project, and its `.editorconfig` tells those rules that EF migrations are generated code. Pass
`--no-build-rules` to leave both out.

It also writes the conventions down for AI coding agents — and for a person new to the code: an `AGENTS.md`,
which every major coding agent reads, a `CLAUDE.md` that imports it for Claude Code,
and an `.ai-knowledge/` folder of guides, one per topic (use cases, entities, events, background services,
testing, style), plus `.ai-knowledge/project.md` for what is specific to your repository. When a newer `mcs`
ships better guides, `mcs update ai-files` brings them in; it overwrites the guides it ships and never touches
`project.md` or any file of your own. Pass `--no-ai-knowledge` to leave them out. Their source is
[AIAgents/](https://github.com/MagicDoorInc/MagicCSharp/tree/master/AIAgents) in the MagicCSharp repository.

| `mcs init` option | |
|---|---|
| `-p, --prefix` | namespace and solution-name root, e.g. `Acme` gives `Acme.Shop.slnx` |
| `--package-version` | MagicCSharp version to pin; defaults to this tool's |
| `--target-framework` | `net10.0` unless you say otherwise |
| `--no-build-rules` | no MagicCSharp.Analyzers reference and no `.editorconfig` |
| `--no-ai-knowledge` | no `AGENTS.md`, `CLAUDE.md` or `.ai-knowledge/` |

## Your own templates

Everything generated comes from a template you can replace, one file at a time:

```bash
mcs templates list
mcs templates eject Entities/dal.cs.hbs
```

Across several repositories, keep the templates in a repository of their own and add it as a submodule at
`.magiccsharp/templates`. See
[template overrides](https://github.com/MagicDoorInc/MagicCSharp/blob/master/docs/template-overrides.md).

The structure this creates is optional; the MagicCSharp packages work in any layout. Full guide:
https://github.com/MagicDoorInc/MagicCSharp/blob/master/docs/repository-layout.md
