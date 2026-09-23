# Acme Leasing — AI guide

A property-management service built on [MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp):
properties, leases, rent, late fees and tenant notifications, written as small use cases chained together.

## Read before working

Start at [`.ai-knowledge/INDEX.md`](.ai-knowledge/INDEX.md) and read the guides its task table names for the
work in front of you. Those guides are the conventions for this repository; where this file, a README or
general C# habit disagrees with them, the guide wins.

When a guide does not cover something, copy the nearest existing code in this repository before reaching for
general industry practice.

## The rules that matter most

- **Never create a project, entity or repository by hand.** Use `mcs` — see
  [`.ai-knowledge/project-tooling.md`](.ai-knowledge/project-tooling.md). It writes the files, the
  registrations and the solution entries so they agree with each other.
- **Business logic lives in use cases**, one operation per class, called `Execute`. No `*Service` classes that
  collect operations.
- **Domain code depends on ports**: repository interfaces, `IEventDispatcher`, `TimeProvider`. Never a `DbContext`,
  a bus client or `DateTime.Now`.
- **Persist first, then dispatch.** An event says something already happened.
- **The build enforces the house style.** `MagicCSharp.Analyzers` turns it into compile errors (`MCS0001`…),
  and `mcs validate` checks what the compiler cannot. A red build is a convention broken, not a nuisance.

## Build, test, run

```bash
dotnet build Acme.All.slnx
dotnet test Acme.All.slnx          # needs Docker: tests run against PostgreSQL in Testcontainers
mcs validate --path .

docker compose up -d               # PostgreSQL and Kafka, for running the service
dotnet ef database update --project Apps/Leasing/Data/Data.EntityFramework
dotnet run --project Apps/Leasing/Leasing.App
```

A change is done when all three pass. Most work only needs the service solution, `Acme.Leasing.slnx`.

## Working with the user

For non-trivial work, start with a short plan. Treat the request as permission for the code and test changes
it needs; ask before widening the scope, making a consequential design decision the request does not settle,
or doing anything destructive or shared (dropping a database, pushing, publishing). Commit only when asked.
