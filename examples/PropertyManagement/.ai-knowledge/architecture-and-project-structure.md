# Architecture and Project Structure

This guide owns where code goes and what may depend on what. How to create the projects is in
`project-tooling.md`; what goes inside a use case is in `use-case-patterns.md`.

## Layout

```text
Apps/
  Leasing/                              one deployable service
    Leasing.App/                        the host: Program.cs and little else
    Leasing.Domains/
      Leases/                           a domain
        Default/                        its use cases and event handlers
        Models/                         its entities, edits, filters, enums
        App/Default/                    its endpoints: controllers, DTOs, hosted services
        Tests/
      Charges/
        Default/ Models/ App/ Tests/
        LateFees/                       a subdomain: the same shape, one level down
    Data/
      Data.Models/                      repository interfaces — no Entity Framework
      Data.EntityFramework/             DALs, EF repositories, the DbContext, migrations
    Leasing.Testing/                    the test base the domains' tests share
Libs/
  Events/                               event contracts
  Web/                                  what every endpoint shares: pagination, id parsing
```

Every project sits in a `Default/`, `Models/`, `Tests/` or `App/Default/` folder, and its name follows its
path: `Leasing.Domains/Charges/LateFees/Default` is `Acme.Leasing.Domains.Charges.LateFees`. `mcs` creates
them that way; do not rename or move one by hand.

## What each project owns

| Project | Holds | Never holds |
|---|---|---|
| `{Domain}/Models` | Entities, edits, filters, enums this domain persists | Behaviour that needs a dependency |
| `{Domain}/Default` | Use cases, event handlers | Controllers, DTOs, `DbContext`, anything HTTP |
| `{Domain}/App/Default` | Controllers, DTOs, hosted services, the domain's DI module | Business decisions |
| `Data/Data.Models` | Repository interfaces | Entity Framework |
| `Data/Data.EntityFramework` | DALs, EF repositories, `MagicLeasingContext`, migrations | Business rules |
| `Leasing.App` | `Program.cs`: calls each module once | Registrations a domain could own itself |

Persistence stays centralised per service. A domain owns its entities in its `Models` project, but its DALs
and repositories live in `Data/`, next to every other table, because they share one database and one
migration history.

## Dependency direction

- `{Domain}.App` → `{Domain}` → `{Domain}.Models`. Never the reverse.
- A domain depends on repository interfaces (`Data.Models`), never on `Data.EntityFramework`.
- **A domain calls another domain through its use cases**, never its repository, DAL or table.
  `Leases` raises charges with `ICreateChargesUseCase`; it does not touch `IChargesRepository`.
- A subdomain may depend on its parent; a parent never depends on its subdomain. `Charges.LateFees` uses
  `Charges`; `Charges` knows nothing about late fees.
- No cycles. Here `Charges.LateFees` → `Leases` → `Charges`. If two domains need each other, one of them is
  doing the other's job, or a small contract belongs in the lower one's `Models` project.

`mcs` does not add references between domains. Which domain may call which is a design decision; add the
reference with `dotnet add <project> reference <project>` when the design calls for it.

## The domains here

| Domain | Owns |
|---|---|
| `Leases` | Properties, leases, signing a lease, tenant notifications |
| `Charges` | What a tenant owes: rent, deposits, payments |
| `Charges.LateFees` | Late-fee policies per property, applying late fees, the hourly background service |

## When to add a domain or subdomain

Separate concepts when they already have different lifecycles, rules or reasons to change — not because two
variants are imaginable. Late fees became a subdomain because they have their own entity (the policy), their
own scheduled work and their own endpoints, and depend on charges without charges depending on them. A single
new use case belongs in the domain whose data it changes.

## Building blocks

Features are made from a closed set of kinds: use cases, event handlers, entities (entity, edit, filter),
repositories and DALs, controllers and DTOs, hosted services, DI modules, and small dependency-free static
strategies. Do not invent a new kind — a `*Service`, `*Manager`, `*Helper` or `*Rules` class that collects
operations — without raising it first. Where a behaviour belongs is in `use-case-patterns.md` → Behaviour
locality.
