namespace MagicCSharp.Infrastructure.Exceptions;

/// <summary>
///     The entity exists and the request is well formed, but the entity's current state does not allow it:
///     paying a charge that is already paid, ending a lease that has ended. The web layer answers it with 422.
///     <para>
///         Distinct from a validation failure (400), which is about the input rather than the state it met.
///     </para>
/// </summary>
public class EntityInvalidOperationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
