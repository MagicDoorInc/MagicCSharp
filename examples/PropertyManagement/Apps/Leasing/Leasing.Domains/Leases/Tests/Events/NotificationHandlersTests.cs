using Acme.Leasing.Domains.Charges.LateFees.UseCases;
using Acme.Leasing.Domains.Charges.Models.Entities;
using Acme.Leasing.Domains.Charges.UseCases;
using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Leasing.Testing;
using Acme.Libraries.Events.Leases;

namespace Acme.Leasing.Domains.Leases.Tests.Events;

/// <summary>
///     Each test does the business thing — sign, pay, run the late-fee job — and asserts only on the
///     notification. Nothing here calls a handler directly; the event is what connects them.
/// </summary>
public class NotificationHandlersTests : LeasingTestBase
{
    private static readonly DateOnly StartDate = new DateOnly(2026, 3, 1);

    public NotificationHandlersTests()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 2, 20, 18, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task SigningALease_QueuesAWelcomeEmail()
    {
        // Arrange
        var property = await CreateProperty();

        // Act
        var signedLease = await SignLease(property, StartDate);

        // Assert
        var notification = Assert.Single(await NotificationsFor(signedLease.Lease.Id));
        Assert.Equal($"welcome_{signedLease.Lease.Id}", notification.Key);
        Assert.Equal("dana@example.com", notification.Recipient);
        Assert.Equal("Welcome to Maple Court", notification.Subject);
    }

    [Fact]
    public async Task PayingACharge_QueuesAReceipt()
    {
        // Arrange
        var property = await CreateProperty();
        var signedLease = await SignLease(property, StartDate);
        var rentCharge = signedLease.Charges.Single(x => x.Type == ChargeType.Rent);
        var payCharge = Resolve<IPayChargeUseCase>();

        // Act
        await payCharge.Execute(rentCharge.Id);

        // Assert
        var notifications = await NotificationsFor(signedLease.Lease.Id);
        var receipt = Assert.Single(notifications, x => x.Key == $"receipt_{rentCharge.Id}");
        Assert.Equal("Payment received", receipt.Subject);
    }

    [Fact]
    public async Task ApplyingALateFee_QueuesALateFeeNotice()
    {
        // Arrange
        var property = await CreateProperty();
        await SetLateFeePolicy(property, graceDays: 5, amount: 75m);
        var signedLease = await SignLease(property, StartDate);
        var applyLateFees = Resolve<IApplyLateFeesUseCase>();

        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 10, 18, 0, 0, TimeSpan.Zero));

        // Act
        await applyLateFees.Execute();

        // Assert
        var notifications = await NotificationsFor(signedLease.Lease.Id);
        var lateFeeNotice = Assert.Single(notifications, x => x.Key.StartsWith("late_fee_"));
        Assert.Contains("75.00", lateFeeNotice.Body);
    }

    [Fact]
    public async Task ARedeliveredEvent_QueuesNothingNew()
    {
        // Arrange
        var property = await CreateProperty();
        var signedLease = await SignLease(property, StartDate);

        // Act
        EventDispatcher.Dispatch(new LeaseSignedEvent
        {
            LeaseId = signedLease.Lease.Id,
            PropertyId = property.Id,
        });

        // Assert
        Assert.Single(await NotificationsFor(signedLease.Lease.Id));
    }

    private async Task<IReadOnlyList<Notification>> NotificationsFor(long leaseId)
    {
        var getNotifications = Resolve<IGetNotificationsUseCase>();

        return await getNotifications.Execute(new NotificationFilter
        {
            LeaseIds = [leaseId],
        });
    }
}
