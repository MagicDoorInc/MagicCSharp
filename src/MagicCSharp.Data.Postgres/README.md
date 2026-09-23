# MagicCSharp.Data.Postgres

PostgreSQL wiring for `MagicCSharp.Data.EntityFramework`: a pooled context factory read from configuration
that fails at startup rather than on the first query, a design-time factory for `dotnet ef`, and an
interceptor for the one `timestamptz` mistake that only ever shows up on a developer's machine.

Add it to the data project alongside `MagicCSharp.Data.EntityFramework` when the database is Postgres.

```bash
dotnet add package MagicCSharp.Data.Postgres
```

## Registration

```csharp
builder.Services.AddPostgresDbContextFactory<MagicShopContext>(builder.Configuration);
```

Reads `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER` and `DB_PASSWORD`. Each is required — a missing one throws
rather than quietly defaulting to localhost. Pass a different `configPrefix` to point a second context at
another database (`VECTOR_DB_HOST`, and so on).

Registers a *pooled* factory, which is what makes the repositories' context-per-operation cheap: pooling
reuses the context and its internal service provider rather than rebuilding the model each time. Opens one
connection during registration, so a wrong host or password fails at startup instead of on the first request
that needs the database; set `DB_VERIFY_CONNECTION=false` where that is wrong — a test that swaps every
repository out, or a container that starts before its database.

Pool sizes, timeouts and retry live in `PostgresConnectionOptions`. Size `MaxPoolSize` against the server's
limit divided by the number of instances you run, or a rolling deploy will exhaust the server.

Two hooks for the provider: `configureDataSource` for an Npgsql type plugin such as pgvector's `UseVector()`,
and `configureNpgsql` for the options inside `UseNpgsql`. A query that `Include`s two collections at once
throws rather than multiplying the rows — that is almost never what was meant, and throwing turns a silent
performance cliff into a fix-once error.

## Migrations

```csharp
public class MagicShopContextFactory() : MagicDbContextFactory<MagicShopContext>("shop");
```

`dotnet ef` builds the context without your DI container, so it needs this. Without one it falls back to
booting the whole host just to read a connection string. The argument is the default database name; host,
port, user and password default to a local Postgres and are overridden by the same `DB_*` variables.

## The UTC interceptor

Registered for you. It normalizes every `DateTimeOffset` command parameter to UTC.

`MagicDbContext` already does that on save, but save only sees entities. A `Where` comparing a timestamp, an
`ExecuteUpdate`, an `ExecuteDelete` and raw SQL all bind parameters without going near the change tracker.
Npgsql requires offset zero for a `timestamptz` parameter, so one of those carrying a local offset throws at
bind time — on a developer's machine, and not in CI, because CI runs in UTC.

## Related packages

- [MagicCSharp.Data.EntityFramework](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data.EntityFramework/README.md)
  — the repository base classes this wires up
- [MagicCSharp.Testing.Database](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing.Database/README.md)
  — repository tests against a real PostgreSQL

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
