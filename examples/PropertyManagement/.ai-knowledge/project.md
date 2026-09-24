# This repository

The MagicCSharp property-management example: one service, `Leasing`, that manages properties, leases, rent,
late fees and tenant notifications. It exists to show the conventions in the other guides working in a real
service, and to prove each MagicCSharp release builds for someone starting from nothing — its CI restores
everything from nuget.org.

## Services and domains

| Service | Domain | Owns |
|---|---|---|
| `Leasing` | `Leases` | Properties, leases, signing a lease, tenant notifications (queued by event handlers) |
| `Leasing` | `Charges` | What a tenant owes: rent, deposits, payments |
| `Leasing` | `Charges.LateFees` | Late-fee policies per property, applying late fees, `ApplyLateFeesBackgroundService` |

Dependencies: `Charges.LateFees` → `Leases` → `Charges`. `Charges` depends on no other domain.

Shared libraries: `Libs/Events` (the event contracts) and `Libs/Web` (pagination and id parsing for endpoints).

Tests share `LeasingTestBase` in `Apps/Leasing/Leasing.Testing`, with `CreateProperty`, `SignLease` and
`SetLateFeePolicy` helpers.

## Running it

```bash
docker compose up -d               # PostgreSQL, and Kafka with the leasing-events topic
dotnet ef database update --project Apps/Leasing/Data/Data.EntityFramework
dotnet run --project Apps/Leasing/Leasing.App      # http://localhost:5200
```

Events go through Kafka (`AddMagicKafkaEvents` in `Program.cs`, settings under `Kafka` in
`appsettings.Development.json`). The service publishes to and consumes from the same topic. Tests do not need
Kafka.

## Differences from the guides

- **No authentication**, so no access-control layer between controllers and use cases. Every endpoint is open.
- **Notifications are queued, not sent.** Nothing hands them to an email provider.
- **Only the first month's rent is raised**, when a lease is signed. There is no monthly rent background
  service.
