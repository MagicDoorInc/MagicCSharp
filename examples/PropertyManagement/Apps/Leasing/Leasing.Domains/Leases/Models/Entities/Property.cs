using MagicCSharp.Infrastructure.Entities;
using MagicCSharp.Infrastructure;

namespace Acme.Leasing.Domains.Leases.Models.Entities;

/// <summary>The Property as the rest of the application sees it.</summary>
public record Property : PropertyEdit, IMagicEntity, IIdEntity
{
    public required long Id { get; init; }
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset Updated { get; init; }
}

/// <summary>The writable fields. Property derives from this, so an entity is accepted wherever an edit is.</summary>
public record PropertyEdit
{
    public required string Name { get; init; }
    public required string Address { get; init; }

    /// <summary>
    ///     An IANA id such as <c>America/Los_Angeles</c>. Rent is due on a calendar day where the property is,
    ///     so every "which day is it" question about a lease is answered in this zone, never in UTC.
    /// </summary>
    public required string TimeZoneId { get; init; }

    public TimeZoneInfo TimeZone => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public DateOnly LocalDate(DateTimeOffset instant)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);
    }
}

/// <summary>Query criteria. Every property is optional; a null one means "do not narrow on this".</summary>
public class PropertyFilter
{
    public List<long>? Ids { get; init; }
    public ComparableRange<DateTimeOffset>? Created { get; init; }
}
