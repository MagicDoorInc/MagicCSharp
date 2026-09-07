# MagicCSharp.Data.Postgres

PostgreSQL wiring for `MagicCSharp.Data.EntityFramework`.

## Registration

```csharp
builder.Services.AddPostgresDbContextFactory<MagicShopContext>(builder.Configuration);
```

Reads `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER` and `DB_PASSWORD`. Each is required — a missing one throws
rather than quietly defaulting to localhost. Pass a different `configPrefix` to point a second context at
another database.

Registers a *pooled* factory, which is what makes the repositories' context-per-operation cheap. Opens one
connection during registration, so a wrong host or password fails at startup instead of on the first request
that needs the database.

Pool sizes, timeouts and retry live in `PostgresConnectionOptions`. Size `MaxPoolSize` against the server's
limit divided by the number of instances you run, or a rolling deploy will exhaust the server.

## Migrations

```csharp
public class MagicShopContextFactory() : MagicDbContextFactory<MagicShopContext>("shop");
```

`dotnet ef` builds the context without your DI container, so it needs this. Without one it falls back to
booting the whole host just to read a connection string.

## The UTC interceptor

Registered for you. It normalizes every `DateTimeOffset` command parameter to UTC.

`MagicDbContext` already does that on save, but save only sees entities. A `Where` comparing a timestamp, an
`ExecuteUpdate`, an `ExecuteDelete` and raw SQL all bind parameters without going near the change tracker.
Npgsql requires offset zero for a `timestamptz` parameter, so one of those carrying a local offset throws at
bind time — on a developer's machine, and not in CI, because CI runs in UTC.
