using MagicCSharp.Events.Events;

namespace Acme.Libraries.Events.Leases;

public record LeaseSignedEvent : MagicEvent
{
    public required long LeaseId { get; init; }
    public required long PropertyId { get; init; }
}
