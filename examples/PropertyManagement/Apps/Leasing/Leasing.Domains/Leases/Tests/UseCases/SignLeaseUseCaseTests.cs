using Acme.Leasing.Domains.Charges.Models.Entities;
using Acme.Leasing.Domains.Charges.UseCases;
using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Leasing.Testing;
using Acme.Libraries.Events.Leases;
using MagicCSharp.Data.Models;
using MagicCSharp.Infrastructure.Exceptions;

namespace Acme.Leasing.Domains.Leases.Tests.UseCases;

public class SignLeaseUseCaseTests : LeasingTestBase
{
    private static readonly DateOnly StartDate = new DateOnly(2026, 3, 1);

    public SignLeaseUseCaseTests()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 2, 20, 18, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Execute_RaisesTheFirstMonthsRentAndTheDeposit()
    {
        // Arrange
        var property = await CreateProperty();
        var getCharges = Resolve<IGetChargesUseCase>();

        // Act
        var signedLease = await SignLease(property, StartDate);

        // Assert
        var charges = await getCharges.Execute(new ChargeFilter
        {
            LeaseIds = [signedLease.Lease.Id],
        });
        Assert.Equal(2, charges.Count);

        var rentCharge = Assert.Single(charges, x => x.Type == ChargeType.Rent);
        Assert.Equal(1850m, rentCharge.Amount);
        Assert.Equal(StartDate, rentCharge.DueDate);

        var depositCharge = Assert.Single(charges, x => x.Type == ChargeType.SecurityDeposit);
        Assert.Equal(1850m, depositCharge.Amount);
        Assert.Equal(StartDate, depositCharge.DueDate);
    }

    [Fact]
    public async Task Execute_RaisesNoDepositCharge_WhenThereIsNoDeposit()
    {
        // Arrange
        var property = await CreateProperty();
        var signLease = Resolve<ISignLeaseUseCase>();

        // Act
        var signedLease = await signLease.Execute(new SignLeaseRequest
        {
            PropertyId = property.Id,
            TenantName = "Dana Whitfield",
            TenantEmail = "dana@example.com",
            MonthlyRent = 1850m,
            SecurityDeposit = 0m,
            StartDate = StartDate,
            EndDate = StartDate.AddYears(1),
        });

        // Assert
        var charge = Assert.Single(signedLease.Charges);
        Assert.Equal(ChargeType.Rent, charge.Type);
    }

    [Fact]
    public async Task Execute_DispatchesLeaseSigned()
    {
        // Arrange
        var property = await CreateProperty();

        // Act
        var signedLease = await SignLease(property, StartDate);

        // Assert
        Assert.True(EventDispatcher.HasDispatchedEvent<LeaseSignedEvent>(leaseSignedEvent =>
            leaseSignedEvent.LeaseId == signedLease.Lease.Id && leaseSignedEvent.PropertyId == property.Id));
    }

    [Fact]
    public async Task Execute_ThrowsNotFound_WhenThePropertyDoesNotExist()
    {
        // Arrange
        var signLease = Resolve<ISignLeaseUseCase>();
        var getLeases = Resolve<IGetLeasesUseCase>();

        // Act
        var signing = signLease.Execute(new SignLeaseRequest
        {
            PropertyId = 404,
            TenantName = "Dana Whitfield",
            TenantEmail = "dana@example.com",
            MonthlyRent = 1850m,
            SecurityDeposit = 1850m,
            StartDate = StartDate,
            EndDate = StartDate.AddYears(1),
        });

        // Assert
        await Assert.ThrowsAsync<NotFoundIdException>(() => signing);

        var leases = await getLeases.Execute(new PaginationRequest(), new LeaseFilter());
        Assert.Equal(0, leases.TotalCount);
    }
}
