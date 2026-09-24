using Acme.Leasing.Domains.Charges.UseCases;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Libraries.Events.Charges;
using MagicCSharp.Events.Events;
using MagicCSharp.Infrastructure.Exceptions;

namespace Acme.Leasing.Domains.Leases.Events;

public class QueueLateFeeNoticeOnLateFeeApplied(
    IGetChargesUseCase getCharges,
    IGetLeasesUseCase getLeases,
    IQueueNotificationUseCase queueNotification) : IEventHandler<LateFeeAppliedEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public async Task Handle(LateFeeAppliedEvent lateFeeAppliedEvent)
    {
        var lateFee = await getCharges.Execute(lateFeeAppliedEvent.LateFeeChargeId);
        NotFoundException.ThrowIfNull(lateFee, lateFeeAppliedEvent.LateFeeChargeId);

        var lease = await getLeases.Execute(lateFeeAppliedEvent.LeaseId);
        NotFoundException.ThrowIfNull(lease, lateFeeAppliedEvent.LeaseId);

        await queueNotification.Execute(new QueueNotificationRequest
        {
            Key = $"late_fee_{lateFee.Id}",
            LeaseId = lease.Id,
            Recipient = lease.TenantEmail,
            Subject = "Your rent is late",
            Body = $"Hi {lease.TenantName}, a late fee of {lateFee.Amount:0.00} has been added to your account.",
        });
    }
}
