using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure;

namespace Acme.Leasing.Domains.Leases.Models.Entities;

/// <summary>
///     A message queued for a tenant. Keyed by what it is about — <c>welcome_{leaseId}</c>,
///     <c>late_fee_{chargeId}</c> — so an event delivered twice finds the first one instead of sending twice.
/// </summary>
public record Notification : NotificationEdit, IMagicEntity, IKeyEntity
{
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

/// <summary>The writable fields. Notification derives from this, so an entity is accepted wherever an edit is.</summary>
public record NotificationEdit
{
    public required string Key { get; init; }
    public required long LeaseId { get; init; }
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
}

/// <summary>Query criteria. Every property is optional; a null one means "do not narrow on this".</summary>
public class NotificationFilter
{
    public List<string>? Keys { get; init; }
    public List<long>? LeaseIds { get; init; }
    public ComparableRange<DateTimeOffset>? Created { get; init; }
}
