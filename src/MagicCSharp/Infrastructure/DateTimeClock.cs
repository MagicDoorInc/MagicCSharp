namespace MagicCSharp.Infrastructure;

/// <summary>
///     Default implementation of IClock that returns the actual system time.
///     Register this as a singleton in your DI container for production use.
/// </summary>
public class DateTimeClock : IClock
{
    /// <summary>
    ///     Gets the current system date and time with timezone offset.
    /// </summary>
    /// <returns>The current DateTimeOffset from the system clock.</returns>
    public DateTimeOffset Now()
    {
        return DateTimeOffset.Now;
    }
}