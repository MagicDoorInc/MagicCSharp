using Acme.Leasing.Domains.Charges.Models.Entities;
using Acme.Leasing.Domains.Charges.UseCases;
using Acme.Leasing.Testing;
using Acme.Libraries.Events.Charges;
using MagicCSharp.Infrastructure.Exceptions;

namespace Acme.Leasing.Domains.Charges.Tests.UseCases;

public class PayChargeUseCaseTests : LeasingTestBase
{
    private static readonly DateOnly StartDate = new DateOnly(2026, 3, 1);

    public PayChargeUseCaseTests()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 2, 18, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Execute_RecordsWhenTheChargeWasPaid()
    {
        // Arrange
        var property = await CreateProperty();
        var signedLease = await SignLease(property, StartDate);
        var rentCharge = signedLease.Charges.Single(x => x.Type == ChargeType.Rent);
        var payCharge = Resolve<IPayChargeUseCase>();

        // Act
        var paidCharge = await payCharge.Execute(rentCharge.Id);

        // Assert
        Assert.Equal(TimeProvider.GetUtcNow(), paidCharge.Paid);
        Assert.True(EventDispatcher.HasDispatchedEvent<ChargePaidEvent>(chargePaidEvent =>
            chargePaidEvent.ChargeId == rentCharge.Id));
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
