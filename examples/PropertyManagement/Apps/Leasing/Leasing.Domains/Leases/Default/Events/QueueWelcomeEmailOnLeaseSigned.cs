using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Libraries.Events.Leases;
using MagicCSharp.Events.Events;
using MagicCSharp.Infrastructure.Exceptions;

namespace Acme.Leasing.Domains.Leases.Events;

public class QueueWelcomeEmailOnLeaseSigned(
    IGetLeasesUseCase getLeases,
    IGetPropertiesUseCase getProperties,
    IQueueNotificationUseCase queueNotification) : IEventHandler<LeaseSignedEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public async Task Handle(LeaseSignedEvent leaseSignedEvent)
    {
        var lease = await getLeases.Execute(leaseSignedEvent.LeaseId);
        NotFoundException.ThrowIfNull(lease, leaseSignedEvent.LeaseId);

        var property = await getProperties.Execute(lease.PropertyId);
        NotFoundException.ThrowIfNull(property, lease.PropertyId);

        await queueNotification.Execute(new QueueNotificationRequest
        {
            Key = $"welcome_{lease.Id}",
            LeaseId = lease.Id,
            Recipient = lease.TenantEmail,
            Subject = $"Welcome to {property.Name}",
            Body = $"Hi {lease.TenantName}, your lease at {property.Address} starts on {lease.StartDate:yyyy-MM-dd}. "
                   + $"Rent is {lease.MonthlyRent:0.00} a month.",
        });
    }
}
