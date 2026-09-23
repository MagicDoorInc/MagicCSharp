using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;
using MagicCSharp.UseCases;
using Microsoft.Extensions.Logging;

namespace Acme.Leasing.Domains.Leases.UseCases;

public interface IQueueNotificationUseCase : IMagicUseCase
{
    Task<Notification> Execute(QueueNotificationRequest request);
}

public record QueueNotificationRequest
{
    /// <summary>What the notification is about. Queueing the same key twice returns the first one.</summary>
    public required string Key { get; init; }

    public required long LeaseId { get; init; }
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
}

/// <summary>
///     Stores the message for a sender to pick up. This example stops there; a real service would have a
///     handler or a job hand it to an email provider.
/// </summary>
public class QueueNotificationUseCase(
    INotificationsRepository notificationsRepository,
    ILogger<QueueNotificationUseCase> logger) : IQueueNotificationUseCase
{
    public async Task<Notification> Execute(QueueNotificationRequest request)
    {
        logger.LogTrace("Executing: request={request}", request);

        // Events arrive at least once. This is what makes a redelivered one harmless.
        var existingNotification = await notificationsRepository.Get(request.Key);
        if (existingNotification != null)
        {
            return existingNotification;
        }

        return await notificationsRepository.Create(new NotificationEdit
        {
            Key = request.Key,
            LeaseId = request.LeaseId,
            Recipient = request.Recipient,
            Subject = request.Subject,
            Body = request.Body,
        });
    }
}
