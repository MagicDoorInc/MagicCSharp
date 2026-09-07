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

Every tool takes `--help`.

## Reference

| Tool | Does | Key options |
|---|---|---|
| `InitRepo` | Sets the repository up. Run once. | `--prefix`, `--package-version` |
| `CreateApp` | New service: host, solution, data projects | `--name`, `--database` / `--no-database`, `--port` |
| `CreateAppLib` | New domain inside a service | `--solution`, `--name`, `--models`, `--tests` |
| `CreateLib` | New shared library under `Libs/` | `--name`, `--tests` |
| `AddEntity` | Entity across its four files, registered | `--solution`, `--domain`, `--name`, `--paginated`, `--use-key` |
| `SyncAllProjects` | Rebuilds `{Prefix}.All.slnx` from disk | — |
| `ValidateConventions` | Lints what the compiler cannot; exits non-zero | `--path` |

## Two guarantees

**Nothing is overwritten.** An existing file is reported and skipped, because regenerating over something you
edited would throw the edits away. Delete a file if you want it regenerated.

**Re-running changes nothing.** Registrations are not duplicated; solutions are rewritten only when the
content differs. Run any tool twice and the second run produces no diff.

`CreateApp`, `CreateAppLib` and `CreateLib` run `SyncAllProjects` themselves.

## Layout of this directory

```
tools/
  Directory.Build.props       pins the scripts to net10.0 — see below
  *.cs                        the tools
  Templates/
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
references) and `GenerateAssemblyCatalog`. The last matters if you split a service across many projects: .NET
loads an assembly only when one of its types is first touched, so use cases in a project the host never
references are not found by registration. Until it is ported, touch one type per project during startup.
