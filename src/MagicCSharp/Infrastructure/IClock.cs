namespace MagicCSharp.Infrastructure;

/// <summary>
///     Provides an abstraction for retrieving the current date and time.
///     This interface enables testable time-dependent code by allowing time to be mocked in tests.
/// </summary>
public interface IClock
{
    /// <summary>
    ///     Gets the current date and time with timezone offset.
    /// </summary>
    /// <returns>The current DateTimeOffset.</returns>
    DateTimeOffset Now();
}