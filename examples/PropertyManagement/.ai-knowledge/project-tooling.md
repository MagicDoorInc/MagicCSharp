# Project Tooling

This guide is the command catalogue. Everything structural — solutions, projects, entities, migrations — is
created by a command, never by hand: the commands keep names, namespaces, references, registrations and
solution entries agreeing with each other, and a hand-made project quietly misses one of them.

`mcs` is the MagicCSharp CLI, installed with `dotnet tool install -g MagicCSharp.Cli`. Every command is safe
to re-run: nothing existing is overwritten, and running it twice changes nothing.

## Domains and subdomains

```bash
# A domain: use cases (Default), entities (Models), tests
mcs create-domain --solution Leasing --name Leases --models --tests

# Its endpoints and hosted services
mcs create-domain --solution Leasing --name Leases.App

# A subdomain, one level down — then reference its parent, which mcs leaves to you
mcs create-domain --solution Leasing --name Charges.LateFees --models --tests
dotnet add Apps/Leasing/Leasing.Domains/Charges/LateFees/Default/Acme.Leasing.Domains.Charges.LateFees.csproj \
  reference Apps/Leasing/Leasing.Domains/Charges/Default/Acme.Leasing.Domains.Charges.csproj
```

A new domain's `Default` project is referenced by the host automatically, so its use cases are registered
without further wiring. References between domains are yours to add, following
`architecture-and-project-structure.md` → Dependency direction.

## Entities

```bash
mcs add-entity --solution Leasing --domain Charges --name Charge --paginated
mcs add-entity --solution Leasing --domain Leases --name Notification --use-key --paginated
```

This writes the entity (`Models/Entities/{Name}.cs`), the DAL, the repository interface and its EF
implementation, the `DbSet`, and the repository's registration. `--use-key` gives a string key instead of a
Snowflake id; `--paginated` adds `IPaginatedRepository`. Then fill in the fields, the DAL columns and the
filter as `entities-and-database.md` describes, and add a migration.

## Migrations

```bash
dotnet ef migrations add AddLateFeePolicies --project Apps/Leasing/Data/Data.EntityFramework
dotnet ef database update --project Apps/Leasing/Data/Data.EntityFramework
```

Name a migration for what it does. Generate one at the end of a change, not per step, and read what it
generated before keeping it. Tests build their schema from the migrations, so a broken one fails the tests.

## Libraries

```bash
mcs create-lib --name Events                          # Libs/: shared by services
mcs create-app-lib --solution Leasing --name Testing  # inside one service, not a domain
```

## Checking

```bash
mcs validate --path .     # the conventions the compiler cannot check
mcs sync                  # rebuild Acme.All.slnx after moving things around
```

Code style is checked by the compiler through `MagicCSharp.Analyzers` (see `coding-style.md`); `mcs
validate` covers the rest, such as DAL columns that need `[Required]`.
