using Acme.Leasing.Domains.Charges.UseCases;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Libraries.Events.Charges;
using MagicCSharp.Events.Events;
using MagicCSharp.Infrastructure.Exceptions;

namespace Acme.Leasing.Domains.Leases.Events;

public class QueueReceiptOnChargePaid(
    IGetChargesUseCase getCharges,
    IGetLeasesUseCase getLeases,
    IQueueNotificationUseCase queueNotification) : IEventHandler<ChargePaidEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public async Task Handle(ChargePaidEvent chargePaidEvent)
    {
        var charge = await getCharges.Execute(chargePaidEvent.ChargeId);
        NotFoundException.ThrowIfNull(charge, chargePaidEvent.ChargeId);

        var lease = await getLeases.Execute(chargePaidEvent.LeaseId);
        NotFoundException.ThrowIfNull(lease, chargePaidEvent.LeaseId);

        await queueNotification.Execute(new QueueNotificationRequest
        {
            Key = $"receipt_{charge.Id}",
            LeaseId = lease.Id,
            Recipient = lease.TenantEmail,
            Subject = "Payment received",
            Body = $"Hi {lease.TenantName}, we received your payment of {charge.Amount:0.00}. Thank you.",
        });
    }
}
