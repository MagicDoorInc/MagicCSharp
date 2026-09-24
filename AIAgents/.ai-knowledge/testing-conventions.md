# Testing Conventions

## What a test here looks like

Tests drive the real use cases against a real PostgreSQL. A service's domain `Tests` projects share one test
base, in a `{Service}.Testing` library (`mcs create-app-lib --solution {Service} --name Testing`), deriving from
`TestRepositoryBase<TContext>` in `MagicCSharp.Testing.Database`. It gives every test class:

- its own database in a shared Testcontainers container, with the schema built by the migrations;
- the service's real use cases, repositories and event handlers, wired as the service wires them;
- three swaps: a `FakeTimeProvider` the test moves, a `SyncEventDispatcher` that runs handlers before `Dispatch`
  returns, and in-memory locks.

The example's [`LeasingTestBase`](https://github.com/MagicDoorInc/MagicCSharp/blob/master/examples/PropertyManagement/Apps/Leasing/Leasing.Testing/Default/LeasingTestBase.cs)
is a complete one to start from. The examples below derive from it.

```csharp
public class PayChargeUseCaseTests : LeasingTestBase
{
    public PayChargeUseCaseTests()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 2, 18, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Execute_Throws_WhenTheChargeIsAlreadyPaid()
    {
        // Arrange
        var property = await CreateProperty();
        var signedLease = await SignLease(property, StartDate);
        var rentCharge = signedLease.Charges.Single(x => x.Type == ChargeType.Rent);
        var payCharge = Resolve<IPayChargeUseCase>();
        await payCharge.Execute(rentCharge.Id);

        // Act
        var payingAgain = payCharge.Execute(rentCharge.Id);

        // Assert
        await Assert.ThrowsAsync<EntityInvalidOperationException>(() => payingAgain);
    }
}
```

Docker must be running. `dotnet test {{ prefix }}.All.slnx` runs everything.

## Rules

- **Test behaviour a user or caller would notice** — charges raised, fees applied once, notifications queued,
  the error a caller gets. Not private structure, log text or which constructor ran.
- **One `[Fact]` per case**, named `{Method}_{Outcome}_{When}` or as a plain sentence. No `[Theory]` rows: a
  failing row should name the behaviour it checked.
- **Arrange, Act, Assert**, marked with comments.
- **Set up through use cases**, the way a caller would: `CreateProperty()`, `SignLease()`,
  `SetLateFeePolicy()` on the example's base. Add a helper there when a second test class needs the same setup.
- **One frozen clock per class**, set in the constructor. Never the wall clock. The fake only moves forward, so
  set the starting point first and advance from there.
- **Assert on what the handlers did**, not only that an event was dispatched, when the reaction is the point.
  `SyncEventDispatcher.HasDispatchedEvent<T>(predicate)` is there when dispatching is itself the effect.

## Scheduled work

Every scheduled workflow gets a test through time:

1. Arrange state that makes the work eligible.
2. Run the use case; assert the persisted result.
3. Move the clock (`TimeProvider.SetUtcNow`, `TimeProvider.Advance`) and run it again.
4. Assert the effect happened once — and did not happen too early.

The example's `ApplyLateFeesUseCaseTests` is the model: the last day of grace (nothing), the day after (one fee), three days
later (still one), paid rent (nothing), and two properties in different time zones at the same instant.

## Before you finish

The affected tests pass, then the whole solution does. Report any test you could not run and why.
