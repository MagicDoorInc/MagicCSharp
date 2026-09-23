using MagicCSharp.Events.Events;

namespace Acme.Libraries.Events.Charges;

public record LateFeeAppliedEvent : MagicEvent
{
    public required long LateFeeChargeId { get; init; }
    public required long RentChargeId { get; init; }
    public required long LeaseId { get; init; }
}
