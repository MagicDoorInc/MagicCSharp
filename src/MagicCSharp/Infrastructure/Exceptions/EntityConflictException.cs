namespace MagicCSharp.Infrastructure.Exceptions;

/// <summary>
///     The operation conflicts with what is already stored: a duplicate of something that must be unique, or a
///     change made against a version that has since moved. The web layer answers it with 409.
///     <para>
///         Thrown by domain code, which knows nothing of HTTP — the mapping lives in MagicCSharp.AspNetCore.
///     </para>
/// </summary>
public class EntityConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
