using Acme.Leasing.Domains.Leases.Models.Entities;

namespace Acme.Leasing.Domains.Leases.App.Models;

public record NotificationDto
{
    public required string Key { get; init; }
    public required string LeaseId { get; init; }
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public required DateTimeOffset Created { get; init; }

    public static NotificationDto FromEntity(Notification notification)
    {
        return new NotificationDto
        {
            Key = notification.Key,
            LeaseId = notification.LeaseId.ToString(),
            Recipient = notification.Recipient,
            Subject = notification.Subject,
            Body = notification.Body,
            Created = notification.Created,
        };
    }
}
