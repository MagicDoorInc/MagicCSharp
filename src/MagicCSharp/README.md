# MagicCSharp

Use cases that register themselves, a clock you can move, ids you know before the insert, and a request id
that follows a request across every `await`. This is the core package: three dependencies, and nothing about
ASP.NET, Entity Framework or Kafka.

Add it when you want business logic as small classes you chain together and construct in a test — and you
are done writing the registration line that goes with each one.

```bash
dotnet add package MagicCSharp
```

## Use cases

A use case is one business operation as a plain class: an interface that extends the `IMagicUseCase` marker,
and an implementation whose constructor names what it depends on.

```csharp
public record PlaceOrderRequest
{
    public required long CustomerId { get; init; }
    public required decimal Total { get; init; }
}

public interface IPlaceOrderUseCase : IMagicUseCase
{
    Task<Order> Execute(PlaceOrderRequest request);
}

public class PlaceOrderUseCase(
    IOrdersRepository ordersRepository,
    IEventDispatcher eventDispatcher) : IPlaceOrderUseCase
{
    public async Task<Order> Execute(PlaceOrderRequest request)
    {
        var order = await ordersRepository.Create(new OrderEdit
        {
            CustomerId = request.CustomerId,
            Total = request.Total,
            Status = OrderStatus.Pending,
        });

        eventDispatcher.Dispatch(new OrderPlacedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
        });

        return order;
    }
}
```

Big work is small use cases called in order. A `CheckoutUseCase` takes `IPlaceOrderUseCase` and
`IAttachPaymentUseCase` in its constructor and calls them one after the other; nothing about that needs the
framework, which is the point. A use case that only sometimes needs another can take
`Lazy<IAttachPaymentUseCase>` instead — it is registered alongside every interface, and is the way out of a
construction cycle between two use cases that call each other conditionally.

### Registration

```csharp
builder.Services.AddMagicCSharp();
```

Scans your assemblies for every interface extending `IMagicUseCase`, registers each implementation under its
interface, and adds `TimeProvider.System` and `IRequestIdHandler`. Snowflake ids are a separate call,
`AddSnowflakeKeyGen()`, because a service that never creates a row has no use for them. `MagicCSharp.App`
makes both calls for you.

Two mistakes that would otherwise surface on the first request are startup errors instead:

- Two implementations of one interface. `GetRequiredService` would silently return whichever was registered
  last; the error names both.
- An interface that extends the marker with no implementation at all.

Lifetime is scoped unless the class says otherwise:

```csharp
[MagicUseCase(ServiceLifetime.Singleton)]
public class WarmCacheUseCase : IWarmCacheUseCase { ... }
```

Every assembly deployed beside the executable is loaded before the scan, so a project the host references but
has not touched yet — a domain holding only event handlers, say — is still found. If you are coming from a
codebase that touches a type from each assembly at startup to force it to load, you do not need that here.
Pass an `assemblyFilter` to narrow the scan, for instance to keep test doubles out of a production container:

```csharp
builder.Services.AddMagicCSharp(assembly => !assembly.GetName().Name!.EndsWith(".Tests"));
```

`AddMagicUseCases()` registers only the use cases, for when you want your own `TimeProvider` or
`IRequestIdHandler`.

### In a controller

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController(IPlaceOrderUseCase placeOrder) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Place([FromBody] PlaceOrderDto placeOrderDto)
    {
        var order = await placeOrder.Execute(new PlaceOrderRequest
        {
            CustomerId = placeOrderDto.CustomerId,
            Total = placeOrderDto.Total,
        });

        return Ok(OrderDto.From(order));
    }
}
```

The controller maps HTTP to a request and a result back to HTTP, and nothing else. Whatever the use case
throws is turned into a problem response by `MagicCSharp.AspNetCore`, so there is no `try`/`catch` here.

### In a test

```csharp
var placeOrder = new PlaceOrderUseCase(new FakeOrdersRepository(), new RecordingEventDispatcher());

var order = await placeOrder.Execute(new PlaceOrderRequest { CustomerId = 7, Total = 42.50m });

Assert.Equal(OrderStatus.Pending, order.Status);
```

No host, no container, no mocking framework. The two fakes are yours — a few lines implementing the
interfaces the use case asked for. `MagicCSharp.Testing` ships doubles for the framework's own seams:
`FakeTimeProvider`, `FakeKeyGen`, `SyncEventDispatcher`.

## Time

.NET's own `TimeProvider` instead of `DateTime.Now`, so a rule about thirty days is tested by moving a clock
rather than by waiting:

```csharp
public class ApplyLateFeesUseCase(
    ILeasesRepository leasesRepository,
    TimeProvider timeProvider) : IApplyLateFeesUseCase
{
    public async Task Execute()
    {
        var cutoff = timeProvider.GetUtcNow().AddDays(-30);
        var overdueLeases = await leasesRepository.Get(new LeaseFilter
        {
            DueBefore = cutoff,
            Status = LeaseStatus.Active,
        });
        ...
    }
}
```

`AddMagicCSharp()` registers `TimeProvider.System`, unless a `TimeProvider` is already registered. In a test,
`FakeTimeProvider` (from Microsoft, brought in by `MagicCSharp.Testing`) moves with `SetUtcNow` and `Advance`
— and, because it is the platform's abstraction, also drives `Task.Delay`, timers and anything else in .NET
that accepts a `TimeProvider`. `MagicCSharp.Analyzers` and `mcs validate` fail a build that reads
`DateTime.Now` or `DateTime.UtcNow` directly.

## Ids

```csharp
builder.Services.AddSnowflakeKeyGen();                // random generator id
builder.Services.AddSnowflakeKeyGen(generatorId: 3);  // one per instance, 0–1023
```

`IKeyGenService.GetId()` returns a 64-bit Snowflake id: 41 bits of milliseconds, 10 bits of generator id, 12
bits of sequence. Time-sortable, so `ORDER BY id` is creation order. Assigned by the application before the
insert, so a caller knows an entity's id without a round trip, and two instances never collide as long as
their generator ids differ — give each production instance its own, or two can issue the same id in the same
millisecond.

`GetKey(length)` is the other shape: an unpredictable Base58 string — no `0`/`O`, no `I`/`l`, so it survives
being read aloud or typed from a screenshot — that carries no timestamp and reveals nothing about how many
exist. Use it for anything a user can see: invite links, webhook targets, API keys. A Snowflake id in a URL
leaks when the record was created and roughly how many there are.

`IsValidId` and `IsValidKey` are cheap syntactic checks for rejecting an obviously malformed route value
before touching the database. They do not prove the id exists.

## Request ids

`IRequestIdHandler` holds the current request id in an `AsyncLocal`, so it survives every `await` in the
request without being passed as a parameter:

```csharp
public class FulfilOrderUseCase(
    IRequestIdHandler requestIdHandler,
    ILogger<FulfilOrderUseCase> logger) : IFulfilOrderUseCase
{
    public Task Execute(long orderId)
    {
        var requestId = requestIdHandler.GetCurrentRequestId();
        logger.LogInformation("Fulfilling {OrderId} for {RequestId}", orderId, requestId);
        ...
    }
}
```

For work that does not begin with an HTTP request — a job, a consumer, a fan-out — set one:

```csharp
using (requestIdHandler.SetRequestId())             // a fresh id
using (requestIdHandler.SetRequestId("abc12345"))   // an id you were given, say from a message
using (requestIdHandler.SetChildRequestId())        // {parent}-{8 chars}, for a sub-operation
```

Each returns a scope that restores the previous id on dispose. The middleware that reads and writes the
`X-Request-ID` header is `UseRequestId()` in `MagicCSharp.AspNetCore`, kept separate so a worker does not take
a dependency on ASP.NET to get this.

## Not found

The domain says "no such thing" without knowing about HTTP:

```csharp
throw new NotFoundIdException(orderId, nameof(Order));
throw new NotFoundKeyException(inviteKey, nameof(Invite));

NotFoundException.ThrowIfNull(order, orderId);             // the same, as a guard
NotFoundException.ThrowIfMissing(orders, requestedIds);    // every requested id must be present
```

`GetDebugData()` returns the id or key as JSON for the log line, so it says which order was missing rather
than only that one was. With `MagicCSharp.AspNetCore` any of these becomes a 404 with a problem+json body.
With `MagicCSharp.Data`, `ordersRepository.GetOrThrow(orderId)` raises the right one for you.

## When the state says no

Two more exceptions let domain code refuse an operation without knowing about HTTP.
`EntityConflictException` is for a clash with what is stored — a duplicate, a stale write.
`EntityInvalidOperationException` is for an entity whose current state does not allow the operation:

```csharp
if (charge.Paid != null)
{
    throw new EntityInvalidOperationException($"Charge {chargeId} is already paid.");
}
```

`MagicCSharp.AspNetCore` answers them with 409 and 422. A `ValidationException` stays the answer for bad
input: 400 means "fix the request", 422 means "the request was fine; the thing it met was not".

## Optional\<T\>

A partial update cannot tell "leave the name alone" from "clear the name" when both arrive as `null`.
`Optional<T>` can:

```csharp
public record UpdateCustomerRequest
{
    public Optional<string?> NickName { get; init; }
}

if (request.NickName.HasValue)
{
    customer.NickName = request.NickName.Value;   // may be null — the caller said so
}
```

`default(Optional<T>)` is the absent case, so a property the deserializer never touched is absent with no
further work. A bare value converts implicitly, so callers write `NickName = "Barbara"`.
`OptionalConverterFactory` makes it round-trip through System.Text.Json; `JsonDefaults.Options` already
includes it, and `MagicCSharp.App` registers it for controllers and minimal APIs.

## ComparableRange\<T\>

A range with inclusive or exclusive ends, for filters:

```csharp
new ComparableRange<DateTimeOffset> { Start = since, End = until, EndInclusive = false }
```

`Contains(value)`, `Contains(range)` and `Overlaps(range)` in memory; `ApplyComparableRangeFilter` in
`MagicCSharp.Data` turns one into a `WHERE` clause.

## Entity markers

| Interface | Adds |
|---|---|
| `IMagicEntity` | `Created`, `Updated` |
| `IIdEntity` | `long Id` |
| `IKeyEntity` | `string Key` |
| `IDeletedEntity` | `DateTimeOffset? Deleted` — null while the entity is live |

The repository contracts in `MagicCSharp.Data` are constrained on these.

## Convention registration for your own markers

What `AddMagicUseCases` does for `IMagicUseCase` is available for any marker interface:

```csharp
services.AddImplementationsOf<IMagicValidator>(ServiceLifetime.Scoped);       // IOrderValidator → OrderValidator
services.AddImplementationsOfBase<IStartupCheck>(typeof(Program).Assembly);   // IEnumerable<IStartupCheck>
```

`AddImplementationsOf` registers each implementation under the sub-interface that extends the marker, with
the same startup errors as use cases; pass `allowMultipleImplementations: true` for a marker that really is
a catalog. `AddImplementationsOfBase` registers everything under the base type itself, scoped to one
assembly so a scan does not collect test doubles.

## Related packages

- [MagicCSharp.App](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.App/README.md)
  — all of this plus ASP.NET, events and scheduling, in two calls
- [MagicCSharp.AspNetCore](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.AspNetCore/README.md)
  — `UseRequestId`, problem-details error handling, startup preflight
- [MagicCSharp.Data](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Data/README.md)
  — repository contracts
- [MagicCSharp.Events](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Events/README.md)
  — `IEventDispatcher` and handlers
- [MagicCSharp.Testing](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.Testing/README.md)
  — `FakeTimeProvider`, `FakeKeyGen`, `SyncEventDispatcher`

The whole picture, and the optional repository layout:
[github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
