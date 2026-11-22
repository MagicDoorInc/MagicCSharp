# Use Case Patterns

A use case is one business operation as a plain class. All business logic lives in use cases: decisions,
validation, state changes, persistence, events and calls to outside systems. This guide owns their shape.

## The shape

```csharp
[GenerateInterface]
[LogExecuting]
public partial class PayChargeUseCase(
    IChargesRepository chargesRepository,
    TimeProvider timeProvider,
    IEventDispatcher eventDispatcher,
    IDistributedLockProvider distributedLockProvider)
{
    /// <summary>Marks the charge paid and tells the tenant.</summary>
    public async Task<Charge> Execute(long chargeId)
    {
        // ...
    }
}
```

- `[GenerateInterface]` makes the compiler write `IPayChargeUseCase : IMagicUseCase` from the public `Execute`
  methods, their doc comments and the `[NotLogged]` marks on their parameters; callers and tests use `IPayChargeUseCase` as usual. `[LogExecuting]` writes the
  `Executing:` line ([Logging](#logging)). Both come from `MagicCSharp.Generators`; when to write the interface or
  the line by hand instead is in `generated-code.md`.
- In a repository that does not reference `MagicCSharp.Generators`, write the interface by hand, as its existing use
  cases do: `public interface IPayChargeUseCase : IMagicUseCase { Task<Charge> Execute(long chargeId); }` and
  `class PayChargeUseCase(...) : IPayChargeUseCase`.
- The interface extends `IMagicUseCase`. `AddMagicCSharp()` finds it and registers it — **never register a use
  case by hand.**
- The class is the interface's name without the `I`. The method is `Execute`, never `ExecuteAsync`.
- Dependencies come through the primary constructor, used directly — no fields copying them.
- **One operation per use case.** Two `Execute` overloads are fine when they are the same operation (by id and
  by entity); two different operations are two classes.
- The class, its `Request`/`Result` records and a hand-written interface live in **one file**, named after the use
  case. A record shared by several use cases moves to its own file.

## Naming dependencies

Named after their type (`MCS0007`, `MCS0020`): a use case drops the `UseCase` suffix, a repository keeps
`Repository`.

| Type | Name |
|---|---|
| `ICreateLeaseUseCase` | `createLease` |
| `IChargesRepository` | `chargesRepository` |
| `TimeProvider` | `timeProvider` |
| `IKeyGenService` | `keyGenService` |
| `IEventDispatcher` | `eventDispatcher` |
| `IDistributedLockProvider` | `distributedLockProvider` |
| `ILogger<T>` | `logger` |

Never `useCase`, `repo` or `svc`. A local holding a use case is named the same way:
`var signLease = Resolve<ISignLeaseUseCase>();` so `signLease.Execute(...)` still reads forty lines later.

## Chaining

Big work is small use cases called in order. `SignLeaseUseCase` finds the property, creates the lease, raises
the day-one charges and dispatches `LeaseSignedEvent` — each step a use case that exists on its own:

```csharp
var property = await getProperties.Execute(request.PropertyId);
NotFoundException.ThrowIfNull(property, request.PropertyId);

var lease = await createLease.Execute(new CreateLeaseRequest { ... });
var charges = await createCharges.Execute(DayOneCharges(lease));

eventDispatcher.Dispatch(new LeaseSignedEvent { LeaseId = lease.Id, PropertyId = property.Id });
```

A chain is not a transaction. If a later step throws, the earlier ones stay done. Keep each step safe to call
again, and put anything that must be all-or-nothing inside one use case (one repository call, one lock).

What must happen for the operation to be true goes in the chain. What should happen as a consequence — an
email, a search index, the next workflow — is an event handler (`events-and-messaging.md`).

## Requests and results

- Records with `required ... { get; init; }` members, never positional (`MCS0001`, `MCS0022`).
- The primary entity's id is the first `Execute` parameter, not a request property:
  `Execute(long propertyId, SetLateFeePolicyRequest request)`.
- More than two business parameters → a request record (`MCS0002` caps a method at four).
- Take plain values — ids, primitives, `DateOnly`, `byte[]` — never HTTP or storage types.

## Reading

One getter per entity, holding the reads other code needs: by id, by ids, by filter, paginated. Add a read
when a caller needs it, not before.

```csharp
public interface IGetChargesUseCase : IMagicUseCase
{
    Task<Charge?> Execute(long chargeId);
    Task<IReadOnlyList<Charge>> Execute(ChargeFilter filter);
    Task<Pagination<Charge>> Execute(PaginationRequest paginationRequest, ChargeFilter filter);
}
```

A getter returns null for a missing row; the caller decides whether that is an error.

## Errors

Throw what the domain means; `MagicCSharp.AspNetCore` turns it into the HTTP status.

| Situation | Throw | Status |
|---|---|---|
| The row does not exist | `NotFoundException.ThrowIfNull(entity, id)`, `ThrowIfMissing(entities, ids)` | 404 |
| The input is wrong | `ValidationException`, `ArgumentOutOfRangeException.ThrowIfNegative(...)` | 400 |
| The entity's state refuses it (paying a paid charge) | `EntityInvalidOperationException` | 422 |
| It clashes with what is stored (a duplicate) | `EntityConflictException` | 409 |

Use cases never throw HTTP exceptions; those are for controllers that genuinely mean a status code.

## Logging

A use case that changes something logs an `Executing:` line with its arguments at trace level. Mark it
`[LogExecuting]` and the compiler writes that line; do not write `logger.LogTrace("Executing: …")` as well (`MCS1103`).

- A read-only one (Get, List, Search, Find, Is, Can, Count) does not log that line: `[LogExecuting]` refuses it
  (`MCS1102`), and a hand-written line in a Get/List/Search use case fails `MCS0015`.
- A parameter that looks like a secret (a password, secret, token or key, a stream, a byte array) fails the build
  (`MCS1101`) until it carries `[NotLogged]`. Never mark a parameter `[NotLogged]` to silence the rule for a value that
  is not secret.
- Without the generators, or for a class `[LogExecuting]` refuses (several use case interfaces, generic, abstract,
  a `ref`/`out`/`in` parameter), take `ILogger<T> logger` and start `Execute` with
  `logger.LogTrace("Executing: request={request}", request);`.

Log warnings and errors when they are worth someone investigating. Take `ILogger<T> logger` for those.

## Persist, then dispatch

Events are dispatched after the change is saved, never before. `Dispatch` returns `void` — do not await it.

## Loading data — red lines

- **No query per item.** Collect the ids, fetch once, look up in a dictionary.
  `ApplyLateFeesUseCase` loads unpaid rent, then all their leases, properties and policies — four queries,
  however many charges.
- **Filter in the database, not in memory.** Put status, date ranges and id lists in the filter.
- **Load the narrowest set.** If you only need ids or a count, ask for ids or a count.

## Concurrency

When correctness depends on one thing happening once — a payment, a fee, a get-or-create — take a
distributed lock around the read-check-write:

```csharp
await using var chargeLock = await distributedLockProvider.AcquireLockAsync($"charge-{chargeId}");
```

## Behaviour locality

| The behaviour is… | Put it in… |
|---|---|
| Used by one use case | a `private static` method in that file |
| A business operation used by several | its own use case |
| Intrinsic to an entity (a calculation, a state check) | the entity itself — `Property.LocalDate(now)` |
| A dependency-free policy (calendar math, lock keys) | a small static class with a narrow name |

Never a `*Service`, `*Helper`, `*Util` or `*Rules` class that grows into a second domain layer. Do not extract a
one-off helper into its own type just to test it; test it through the use case.

## Controllers call use cases

A controller converts the HTTP shape to a request, calls a use case, converts the result back. No decisions,
no repositories, no events. See `api-and-controller-conventions.md`. Access control and caller
identity belong in a thin layer of use cases between controllers and these, so business use cases stay
callable from background services, handlers and tests where there is no HTTP caller.
