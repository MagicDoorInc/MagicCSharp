# Architecture and Project Structure

This guide owns where code goes and what may depend on what. How to create the projects is in
`project-tooling.md`; what goes inside a use case is in `use-case-patterns.md`. This repository's own services
and domains are listed in `project.md`.

## Services

A repository holds several **domain services**: each app under `Apps/` is deployed on its own and owns a whole
business domain — its use cases, its data, its endpoints and its background work. Services share the libraries
under `Libs/`, the tooling and these conventions, but not their data:

- A service never reads or writes another service's database, and never references another service's projects.
- Services talk through events, whose contracts live in a shared library under `Libs/`.
- Code more than one service needs goes in `Libs/`; code one service needs stays in that service.

New work belongs in the service whose domain it changes. A new service is a deliberate decision — a business
domain with its own data and its own reason to deploy — not a way to keep a change small.

## Layout

```text
Apps/
  {Service}/                            one deployable service
    {Service}.App/                      the host: Program.cs and little else
    {Service}.Domains/
      {Domain}/                         a domain
        Default/                        its use cases and event handlers
        Models/                         its entities, edits, filters, enums
        App/Default/                    its endpoints: controllers, DTOs, background services, its DI module
        Tests/
        {Subdomain}/                    a subdomain: the same shape, one level down
    Data/
      Data.Models/                      repository interfaces — no Entity Framework
      Data.EntityFramework/             DALs, EF repositories, the DbContext, migrations
    {Service}.Testing/                  the test base the domains' tests share, when there is one
Libs/
  {Library}/Default/                    shared by services: event contracts, HTTP helpers, clients
```

Every project sits in a `Default/`, `Models/`, `Tests/` or `App/Default/` folder, and its name follows its path:
`Leasing.Domains/Charges/LateFees/Default` is `Acme.Leasing.Domains.Charges.LateFees`. `mcs` creates
them that way; do not rename or move one by hand.

## What each project owns

| Project | Holds | Never holds |
|---|---|---|
| `{Domain}/Models` | Entities, edits, filters, enums this domain persists | Behaviour that needs a dependency |
| `{Domain}/Default` | Use cases, event handlers | Controllers, DTOs, a `DbContext`, anything HTTP |
| `{Domain}/App/Default` | Controllers, DTOs, background services, the domain's DI module | Business decisions |
| `Data/Data.Models` | Repository interfaces | Entity Framework |
| `Data/Data.EntityFramework` | DALs, EF repositories, the `DbContext`, migrations | Business rules |
| `{Service}.App` | `Program.cs`: calls each module once | Registrations a domain could own itself |

Persistence stays centralised per service. A domain owns its entities in its `Models` project, but its DALs and
repositories live in `Data/`, next to every other table, because they share one database and one migration
history.

## Dependency direction

- `{Domain}.App` → `{Domain}` → `{Domain}.Models`. Never the reverse.
- A domain depends on repository interfaces (`Data.Models`), never on `Data.EntityFramework`.
- **A domain calls another domain through its use cases**, never its repository, DAL or table. In the example,
  `Leases` raises charges with `ICreateChargesUseCase`; it does not touch `IChargesRepository`.
- A subdomain may depend on its parent; a parent never depends on its subdomain. `Charges.LateFees` uses
  `Charges`; `Charges` knows nothing about late fees.
- No cycles. If two domains need each other, one of them is doing the other's job, or a small contract belongs
  in the lower one's `Models` project.

`mcs` does not add references between domains. Which domain may call which is a design decision; add the
reference with `dotnet add <project> reference <project>` when the design calls for it.

## When to add a domain or subdomain

Separate concepts when they already have different lifecycles, rules or reasons to change — not because two
variants are imaginable. In the example, late fees are a subdomain of charges because they have their own
entity (the policy), their own scheduled work and their own endpoints, and depend on charges without charges
depending on them. A single new use case belongs in the domain whose data it changes.

## Building blocks

Features are made from a closed set of kinds: use cases, event handlers, entities (entity, edit, filter),
repositories and DALs, controllers and DTOs, background services, DI modules, and small dependency-free static
strategies. Do not invent a new kind — a `*Service`, `*Manager`, `*Helper` or `*Rules` class that collects
operations — without raising it first. Where a behaviour belongs is in `use-case-patterns.md` → Behaviour
locality.
