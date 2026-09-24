# Acme — guide for AI coding agents

A repository built on [MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp): business logic as small use
cases chained together, apps (each a deployable service) split into domains, and the house style enforced by
the build.

## Read before working

Start at [`.ai-knowledge/INDEX.md`](.ai-knowledge/INDEX.md) and read the guides its task table names for the
work in front of you. Those guides are the conventions for this repository; where this file, a README or
general C# habit disagrees with them, the guide wins. What is specific to this repository — its services,
domains, and anything that overrides a guide — is in [`.ai-knowledge/project.md`](.ai-knowledge/project.md).

When a guide does not cover something, copy the nearest existing code in this repository before reaching for
general industry practice.

## The rules that matter most

- **Never create a project, entity or repository by hand.** Use `mcs` — see
  [`.ai-knowledge/project-tooling.md`](.ai-knowledge/project-tooling.md). It writes the files, the
  registrations and the solution entries so they agree with each other.
- **Business logic lives in use cases**, one operation per class, called `Execute`. No `*Service` classes that
  collect operations.
- **Domain code depends on ports**: repository interfaces, `IEventDispatcher`, `TimeProvider`. Never a
  `DbContext`, a bus client or `DateTime.Now`.
- **Persist first, then dispatch.** An event says something already happened.
- **The build enforces the house style.** `MagicCSharp.Analyzers` turns it into compile errors (`MCS0001`…),
  and `mcs validate` checks what the compiler cannot. A red build is a convention broken, not a nuisance.

## Build and test

```bash
dotnet build Acme.All.slnx
dotnet test Acme.All.slnx          # repository tests need Docker: they run against PostgreSQL
mcs validate --path .
```

A change is done when all three pass. Most work only needs one service's solution, `Acme.{Service}.slnx`.

## Working with the user

For non-trivial work, start with a short plan. Treat the request as permission for the code and test changes
it needs; ask before widening the scope, making a consequential design decision the request does not settle,
or doing anything destructive or shared (dropping a database, pushing, publishing). Commit only when asked.

## These files

This file is `AGENTS.md`, the name most coding agents look for; `CLAUDE.md` imports it, so Claude Code reads
the same text. Both, and the guides in `.ai-knowledge/`, come from MagicCSharp and are refreshed by `mcs update
ai-files`, which overwrites them with the version that ships with your `mcs`. Keep what is specific to this
repository in `.ai-knowledge/project.md`, or in files of your own under `.ai-knowledge/` — the update never
touches those.
