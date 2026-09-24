using Acme.Leasing.Domains.Charges.LateFees.UseCases;
using Acme.Leasing.Domains.Charges.Models.Entities;
using Acme.Leasing.Domains.Charges.UseCases;
using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Leasing.Testing;
using Acme.Libraries.Events.Charges;

namespace Acme.Leasing.Domains.Charges.LateFees.Tests.UseCases;

/// <summary>
///     The hourly job's work, driven through time: rent due on 1 March in Los Angeles, five days' grace, a
///     75.00 fee. The clock is moved rather than waited on, and the use case is run again the way the job
///     would run it, to show a fee is charged once however often it runs.
/// </summary>
public class ApplyLateFeesUseCaseTests : LeasingTestBase
{
    private static readonly DateOnly RentDueDate = new DateOnly(2026, 3, 1);
    private static readonly TimeSpan PacificStandardTime = TimeSpan.FromHours(-8);

    public ApplyLateFeesUseCaseTests()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 1, 10, 0, 0, PacificStandardTime));
    }

    [Fact]
    public async Task Execute_ChargesNothing_OnTheLastDayOfTheGracePeriod()
    {
        // Arrange
        var property = await CreatePropertyWithPolicy();
        await SignLease(property, RentDueDate);
        var applyLateFees = Resolve<IApplyLateFeesUseCase>();

        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 6, 23, 0, 0, PacificStandardTime));

        // Act
        var result = await applyLateFees.Execute();

        // Assert
        Assert.Equal(0, result.LateFeesApplied);
    }

    [Fact]
    public async Task Execute_ChargesTheLateFee_TheDayAfterTheGracePeriod()
    {
        // Arrange
        var property = await CreatePropertyWithPolicy();
        var signedLease = await SignLease(property, RentDueDate);
        var rentCharge = signedLease.Charges.Single(x => x.Type == ChargeType.Rent);
        var applyLateFees = Resolve<IApplyLateFeesUseCase>();

        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 7, 1, 0, 0, PacificStandardTime));

        // Act
        var result = await applyLateFees.Execute();

        // Assert
        Assert.Equal(1, result.LateFeesApplied);

        var lateFee = Assert.Single(await LateFees());
        Assert.Equal(75m, lateFee.Amount);
        Assert.Equal(rentCharge.Id, lateFee.LateFeeForChargeId);
        Assert.Equal(new DateOnly(2026, 3, 7), lateFee.DueDate);
        Assert.True(EventDispatcher.HasDispatchedEvent<LateFeeAppliedEvent>(lateFeeAppliedEvent =>
            lateFeeAppliedEvent.RentChargeId == rentCharge.Id));
    }

    [Fact]
    public async Task Execute_ChargesTheLateFeeOnce_WhenItRunsAgainLater()
    {
        // Arrange
        var property = await CreatePropertyWithPolicy();
        await SignLease(property, RentDueDate);
        var applyLateFees = Resolve<IApplyLateFeesUseCase>();

        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 7, 1, 0, 0, PacificStandardTime));
        await applyLateFees.Execute();

        TimeProvider.Advance(TimeSpan.FromDays(3));

        // Act
        var result = await applyLateFees.Execute();

        // Assert
        Assert.Equal(0, result.LateFeesApplied);
        Assert.Single(await LateFees());
    }

    [Fact]
    public async Task Execute_ChargesNothing_WhenTheRentIsPaid()
    {
        // Arrange
        var property = await CreatePropertyWithPolicy();
        var signedLease = await SignLease(property, RentDueDate);
        var rentCharge = signedLease.Charges.Single(x => x.Type == ChargeType.Rent);
        var payCharge = Resolve<IPayChargeUseCase>();
        await payCharge.Execute(rentCharge.Id);
        var applyLateFees = Resolve<IApplyLateFeesUseCase>();

        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 10, 9, 0, 0, PacificStandardTime));

        // Act
        var result = await applyLateFees.Execute();

        // Assert
        Assert.Equal(0, result.LateFeesApplied);
    }

    [Fact]
    public async Task Execute_ChargesNothing_WhenThePropertyHasNoPolicy()
    {
        // Arrange
        var property = await CreateProperty();
        await SignLease(property, RentDueDate);
        var applyLateFees = Resolve<IApplyLateFeesUseCase>();

        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 20, 9, 0, 0, PacificStandardTime));

        // Act
        var result = await applyLateFees.Execute();

        // Assert
        Assert.Equal(0, result.LateFeesApplied);
    }

    [Fact]
    public async Task Execute_DecidesLatenessInEachPropertysOwnTimeZone()
    {
        // Arrange
        var losAngelesProperty = await CreatePropertyWithPolicy("America/Los_Angeles");
        var aucklandProperty = await CreatePropertyWithPolicy("Pacific/Auckland");
        await SignLease(losAngelesProperty, RentDueDate);
        var aucklandLease = await SignLease(aucklandProperty, RentDueDate);
        var applyLateFees = Resolve<IApplyLateFeesUseCase>();

        // Noon UTC on 6 March: 7 March in Auckland, past the grace period; still 6 March in Los Angeles.
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));

        // Act
        var result = await applyLateFees.Execute();

        // Assert
        Assert.Equal(1, result.LateFeesApplied);

        var lateFee = Assert.Single(await LateFees());
        Assert.Equal(aucklandLease.Lease.Id, lateFee.LeaseId);
    }

    private async Task<Property> CreatePropertyWithPolicy(string timeZoneId = "America/Los_Angeles")
    {
        var property = await CreateProperty(timeZoneId);
        await SetLateFeePolicy(property, graceDays: 5, amount: 75m);

        return property;
    }

    private async Task<IReadOnlyList<Charge>> LateFees()
    {
        var getCharges = Resolve<IGetChargesUseCase>();

        return await getCharges.Execute(new ChargeFilter
        {
            Types = [ChargeType.LateFee],
        });
    }
}
