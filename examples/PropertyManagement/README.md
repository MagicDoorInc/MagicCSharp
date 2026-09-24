# MagicCSharp example: property management

A leasing service built with [MagicCSharp](../../README.md) the way MagicDoor builds its own: properties,
leases, rent, late fees and tenant notifications, as small use cases chained together — no `LeaseService`, no
`ChargeService`.

Everything here was scaffolded by the published `mcs` CLI and restores the published NuGet packages — never
this repository's source. [CI](../../.github/workflows/example.yml) builds it from nuget.org on every change
and every week, so if it is green, the release works.

## What it shows

**Big work is small use cases in order.** Signing a lease is four steps, each an operation that exists on its
own:

```csharp
public class SignLeaseUseCase(
    IGetPropertiesUseCase getProperties,
    ICreateLeaseUseCase createLease,
    ICreateChargesUseCase createCharges,
    IEventDispatcher eventDispatcher,
    ILogger<SignLeaseUseCase> logger) : ISignLeaseUseCase
{
    public async Task<SignLeaseResult> Execute(SignLeaseRequest request)
    {
        logger.LogTrace("Executing: request={request}", request);

        var property = await getProperties.Execute(request.PropertyId);
        NotFoundException.ThrowIfNull(property, request.PropertyId);

        var lease = await createLease.Execute(new CreateLeaseRequest { ... });

        var charges = await createCharges.Execute(DayOneCharges(lease));

        eventDispatcher.Dispatch(new LeaseSignedEvent
        {
            LeaseId = lease.Id,
            PropertyId = property.Id,
        });

        return new SignLeaseResult
        {
            Lease = lease,
            Charges = charges,
        };
    }
}
```

[`SignLeaseUseCase.cs`](Apps/Leasing/Leasing.Domains/Leases/Default/UseCases/SignLeaseUseCase.cs)

The steps are not a transaction. If raising the charges failed, the lease would already exist — which is why
each step is an operation you can call again on its own, rather than a private method you cannot.

**Side work is an event, not a step.** The welcome email is not in that chain. It is
[`QueueWelcomeEmailOnLeaseSigned`](Apps/Leasing/Leasing.Domains/Leases/Default/Events/QueueWelcomeEmailOnLeaseSigned.cs),
a handler. A lease is signed whether or not the email goes, and adding a second reaction to signing is adding
a class, not editing `SignLeaseUseCase`. The events go through **Kafka** — `compose.yaml` runs a broker — and
switching transport is one line in `Program.cs`; no publisher or handler knows which one it is on. Handlers are
safe to run twice, because Kafka delivers at least once: a notification is keyed by what it is about
(`welcome_{leaseId}`), so a redelivered event finds the first one.

**Scheduled work calls a use case.**
[`ApplyLateFeesBackgroundService`](Apps/Leasing/Leasing.Domains/Charges/LateFees/App/Default/BackgroundServices/ApplyLateFeesBackgroundService.cs)
runs hourly and does one thing: call
[`ApplyLateFeesUseCase`](Apps/Leasing/Leasing.Domains/Charges/LateFees/Default/UseCases/ApplyLateFeesUseCase.cs).
The late-fees domain registers it itself, through `LateFeesAppModule`, so `Program.cs` stays a list of one-line
calls. Everything that decides anything is in the use case:

- "Late" is decided in each property's own time zone. Rent due on the 1st is late in Auckland hours before it
  is late in Los Angeles.
- It loads what it needs in batches — the unpaid rent, then the leases, properties and policies for all of
  it — never a query per charge.
- It is safe to run as often as you like. A rent charge that already has a late fee is skipped, under a lock,
  so the background service and the manual trigger can overlap without charging twice.

**Tests move time instead of waiting for it.**
[`ApplyLateFeesUseCaseTests`](Apps/Leasing/Leasing.Domains/Charges/LateFees/Tests/UseCases/ApplyLateFeesUseCaseTests.cs)
signs a lease, moves .NET's `FakeTimeProvider` to the last day of the grace period, then the day after, then
three days later, and runs the use case each time:

```csharp
TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 7, 1, 0, 0, PacificStandardTime));
await applyLateFees.Execute();

TimeProvider.Advance(TimeSpan.FromDays(3));

var result = await applyLateFees.Execute();

Assert.Equal(0, result.LateFeesApplied);
Assert.Single(await LateFees());
```

**The house style is a build error.** `mcs init` added `MagicCSharp.Analyzers` to every project, so a
positional record, a missing brace, `DateTime.Now` or a variable called `useCase` stops the build. `mcs
validate` in CI covers what the compiler cannot.

## The shape

```
Apps/Leasing/
  Leasing.App/                     Program.cs — the host, and little else
  Leasing.Domains/
    Leases/                        properties, leases, signing, tenant notifications
      Default/                       use cases and event handlers
      Models/                        entities
      App/Default/                   endpoints
      Tests/
    Charges/                       rent, deposits, payments
      LateFees/                    a subdomain: its policy, use cases, background service, endpoints, tests
  Data/
    Data.Models/                   repository interfaces — no Entity Framework
    Data.EntityFramework/          DALs, repositories, context, the migration
  Leasing.Testing/                 the test base every domain's tests share
Libs/
  Events/                          event contracts
  Web/                             pagination and id parsing for the endpoints
.ai-knowledge/                     the conventions, one guide per topic, from mcs — start at INDEX.md
  project.md                       what is specific to this service
CLAUDE.md                          where an AI coding agent starts, from mcs
compose.yaml                       PostgreSQL and Kafka
```

A domain depends on another domain's use cases, never its repository. `Leases` raises charges through
`ICreateChargesUseCase`; `Charges` knows nothing about leases. `LateFees` depends on its parent, `Charges`, and
never the other way round.

## Working on it with an AI agent

[`CLAUDE.md`](CLAUDE.md) and [`.ai-knowledge/`](.ai-knowledge/INDEX.md) are the conventions this service is
written to — where a use case goes, how an entity is shaped, how events and background services work, how
tests move time — written for an agent to read before it changes anything, and useful to a person for the same
reason. `mcs init` wrote them, as it does into every new repository, and `mcs update ai-files` brings them up
to date. What is specific to this service is in [`.ai-knowledge/project.md`](.ai-knowledge/project.md), which
the update never touches.

## How it was made

Every project, entity, repository and registration came from `mcs` — which is the point: this example doubles
as a check that the CLI produces something that builds.

```bash
dotnet tool install -g MagicCSharp.Cli

mcs init --prefix Acme
mcs create-app --name Leasing --database leasing
mcs create-lib --name Events
mcs create-lib --name Web

mcs create-domain --solution Leasing --name Leases --models --tests
mcs create-domain --solution Leasing --name Charges --models --tests
mcs create-domain --solution Leasing --name Charges.LateFees --models --tests
mcs create-domain --solution Leasing --name Leases.App
mcs create-domain --solution Leasing --name Charges.App
mcs create-domain --solution Leasing --name Charges.LateFees.App
mcs create-app-lib --solution Leasing --name Testing

mcs add-entity --solution Leasing --domain Leases --name Property --paginated
mcs add-entity --solution Leasing --domain Leases --name Lease --paginated
mcs add-entity --solution Leasing --domain Leases --name Notification --use-key --paginated
mcs add-entity --solution Leasing --domain Charges --name Charge --paginated
mcs add-entity --solution Leasing --domain Charges.LateFees --name LateFeePolicy

dotnet ef migrations add CreateLeasingTables --project Apps/Leasing/Data/Data.EntityFramework
```

What was written by hand: the fields on each entity and DAL, the filters, the use cases, the handlers, the
endpoints, the background service and its module, the tests, `compose.yaml` — and the project references
between domains, which `mcs` leaves to you on purpose, because which domain may call which is a design
decision, not a default.

## Running it

You need the .NET 10 SDK and Docker. From `examples/PropertyManagement`:

```bash
docker compose up -d
dotnet ef database update --project Apps/Leasing/Data/Data.EntityFramework
dotnet run --project Apps/Leasing/Leasing.App
```

The service listens on `http://localhost:5200`, and reads PostgreSQL and Kafka from
`appsettings.Development.json`. Set the lease's `startDate` to the first of the current month and, from the 7th
on, this shows the whole flow, late fee included:

```bash
# A property, and its late-fee policy: five days' grace, then 75.00
curl -X POST localhost:5200/properties -H 'content-type: application/json' \
  -d '{"name":"Maple Court","address":"12 Maple Court, Portland, OR","timeZoneId":"America/Los_Angeles"}'
curl -X PUT localhost:5200/late-fees/policies/{propertyId} -H 'content-type: application/json' \
  -d '{"graceDays":5,"amount":75}'

# Sign a lease — raises the rent and the deposit, and publishes LeaseSigned to Kafka
curl -X POST localhost:5200/leases -H 'content-type: application/json' \
  -d '{"propertyId":"{propertyId}","tenantName":"Dana Whitfield","tenantEmail":"dana@example.com",
       "monthlyRent":1850,"securityDeposit":1850,"startDate":"2026-09-01","endDate":"2027-08-31"}'

# Run what the hourly background service runs. Run it twice: the second charges nothing.
curl -X POST localhost:5200/late-fees/apply

curl "localhost:5200/charges?leaseIds={leaseId}"
curl -X POST localhost:5200/charges/{chargeId}/pay          # a second time answers 422
curl "localhost:5200/notifications?leaseIds={leaseId}"     # queued by the handlers, via Kafka
```

Ids come back as strings: they are 64-bit, and JavaScript numbers are not. `docker compose down -v` stops
everything and discards the data.

## Tests

```bash
dotnet test Acme.All.slnx
```

Each test class gets its own PostgreSQL database in a shared Testcontainers container, with the schema built
by the migrations, and the service's real use cases, repositories and handlers. Three things are swapped, in
[`LeasingTestBase`](Apps/Leasing/Leasing.Testing/Default/LeasingTestBase.cs): the clock is a
`FakeTimeProvider` the test moves; events run their handlers before `Dispatch` returns, so a test can assert on
what they did and needs no Kafka; and locks are in-memory.

## What it leaves out

- **Authentication.** Every endpoint is open. A real service puts its auth policy and caller identity in front
  of these use cases; MagicDoor adds a layer of use cases for that between controllers and these.
- **Sending email.** Notifications are queued, not sent. A handler or background service that hands them to a
  provider is the missing piece.
- **Monthly rent.** Signing raises the first month; a background service that raises each following month
  would look like `ApplyLateFeesBackgroundService`.
