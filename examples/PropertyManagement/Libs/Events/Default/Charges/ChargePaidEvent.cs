using MagicCSharp.Events.Events;

namespace Acme.Libraries.Events.Charges;

public record ChargePaidEvent : MagicEvent
{
    public required long ChargeId { get; init; }
    public required long LeaseId { get; init; }
}
