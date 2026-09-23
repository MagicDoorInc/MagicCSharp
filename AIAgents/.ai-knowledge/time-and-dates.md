# Time and Dates

## The current time

Always `timeProvider.GetUtcNow()`, from an injected `TimeProvider` — .NET's own clock abstraction.
`DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now` and `DateTimeOffset.UtcNow` do not compile outside a
`TimeProvider` implementation (`MCS0008`) — tests included. A static helper takes
`DateTimeOffset now` from its caller. Measure elapsed time with `Stopwatch`.

## Instants and calendar days

Two different things, stored differently:

| It is… | Type | Example |
|---|---|---|
| A moment | `DateTimeOffset`, stored UTC (`timestamptz`) | `Created`, `Paid` |
| A day on a calendar | `DateOnly` (`date`) | a lease's `StartDate`, a charge's `DueDate` |

`MagicDbContext` normalises every `DateTimeOffset` to UTC on save.

## Which day is it?

A calendar day belongs to a place. Rent due on the 1st is due on the 1st **where the property is**, so any
"which day is it" question is answered in the property's time zone, never in UTC:

```csharp
var today = property.LocalDate(timeProvider.GetUtcNow());
var isLate = today > rentCharge.DueDate.AddDays(lateFeePolicy.GraceDays);
```

`Property.TimeZoneId` is an IANA id such as `America/Los_Angeles`, checked when the property is created.

Comparing two instants, or adding a duration to one, needs no time zone — they are absolute. Anything about
days, months or "the 1st" does.

## Never

- Build a boundary with `new DateTimeOffset(..., TimeSpan.Zero)` and call it a local day.
- Reuse one value's `Offset` to build another; daylight saving moves it.
- Format a UTC instant as a local date without converting it first.
