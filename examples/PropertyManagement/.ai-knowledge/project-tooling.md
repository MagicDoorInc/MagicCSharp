# Project Tooling

This guide is the command catalogue. Everything structural — solutions, projects, entities, migrations — is
created by a command, never by hand: the commands keep names, namespaces, references, registrations and
solution entries agreeing with each other, and a hand-made project quietly misses one of them.

`mcs` is the MagicCSharp CLI, installed with `dotnet tool install -g MagicCSharp.Cli`. Every command is safe to
re-run: nothing existing is overwritten, and running it twice changes nothing. `mcs <command> --help` lists
every option.

## Services

```bash
mcs create-app --name {Service} --database {database}
mcs create-app --name {Service} --no-database
```

## Domains and subdomains

```bash
# A domain: use cases (Default), entities (Models), tests
mcs create-domain --solution {Service} --name {Domain} --models --tests

# Its endpoints and background services
mcs create-domain --solution {Service} --name {Domain}.App

# A subdomain, one level down — then reference its parent, which mcs leaves to you
mcs create-domain --solution {Service} --name {Domain}.{Subdomain} --models --tests
dotnet add <the subdomain's Default csproj> reference <the parent's Default csproj>
```

A new domain's `Default` project is referenced by the host automatically, so its use cases are registered
without further wiring. References between domains are yours to add, following
`architecture-and-project-structure.md` → Dependency direction.

## Entities

```bash
mcs add-entity --solution {Service} --domain {Domain} --name {Entity} --paginated
mcs add-entity --solution {Service} --domain {Domain} --name {Entity} --use-key --paginated
```

This writes the entity (`Models/Entities/{Entity}.cs`), the DAL, the repository interface and its EF
implementation, the `DbSet`, and the repository's registration. `--use-key` gives a string key instead of a
Snowflake id; `--paginated` adds `IPaginatedRepository`. Then fill in the fields, the DAL columns and the filter
as `entities-and-database.md` describes, and add a migration.

## Migrations

```bash
dotnet ef migrations add {WhatItDoes} --project Apps/{Service}/Data/Data.EntityFramework
dotnet ef database update --project Apps/{Service}/Data/Data.EntityFramework
```

Name a migration for what it does. Generate one at the end of a change, not per step, and read what it
generated before keeping it. Build a test's schema from the migrations, so a broken one fails the tests.

## Libraries

```bash
mcs create-lib --name {Library}                              # Libs/: shared by services
mcs create-app-lib --solution {Service} --name {Library}      # inside one service, not a domain
```

## Checking and maintenance

```bash
mcs validate --path .     # the conventions the compiler cannot check
mcs sync                  # rebuild Acme.All.slnx after moving things around
mcs update ai-files       # refresh AGENTS.md and these guides to the version your mcs ships
mcs references --project Apps/{Service}/{Service}.App/Acme.{Service}.App.csproj   # what a project depends on
mcs affected --base origin/master   # which services the committed changes on this branch reach, as CI sees it
```

A change to a shared library under `Libs/` reaches every service whose host project depends on it, and CI
rebuilds, retests and redeploys each of them. Before changing one — an event contract especially — check which
services those are with `mcs references` on each host project, and tell the user.

Code style is checked by the compiler through `MagicCSharp.Analyzers` (see `coding-style.md`); `mcs validate`
covers the rest, such as DAL columns that need `[Required]`.
