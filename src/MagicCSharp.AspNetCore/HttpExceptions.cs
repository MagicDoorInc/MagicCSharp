namespace MagicCSharp.AspNetCore;

/// <summary>
///     An exception that already knows which status code it means.
///     <para>
///         Throw one of these from a use case when the HTTP shape of a failure is genuinely part of the
///         decision — a caller sent something contradictory, a precondition failed. For the ordinary cases,
///         throw the domain exception instead: <c>NotFoundException</c> from MagicCSharp already maps to
///         404, and a use case that throws it stays usable from a queue consumer or a console app, which one
///         of these would not.
///     </para>
/// </summary>
public abstract class HttpException(int statusCode, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public int StatusCode { get; } = statusCode;

    /// <summary>A short, stable label for the kind of failure, used as the problem's title.</summary>
    public virtual string Title => GetType().Name.Replace("Exception", "");
}

/// <summary>400 — the request itself is malformed or self-contradictory.</summary>
public class BadRequestException(string message, Exception? inner = null) : HttpException(400, message, inner);

/// <summary>401 — no credentials, or credentials that are not valid.</summary>
public class UnauthorizedException(string message = "Unauthorized", Exception? inner = null) : HttpException(401, message, inner);

/// <summary>403 — authenticated, but not allowed to do this.</summary>
public class ForbiddenException(string message = "Forbidden", Exception? inner = null) : HttpException(403, message, inner);

/// <summary>
///     404 — no such thing. Prefer the domain <c>NotFoundException</c>, which maps here anyway; this exists
///     for a controller that has no domain call to make. Named Http-first because an unprefixed
///     NotFoundException collides with the domain one in any file that uses both.
/// </summary>
public class HttpNotFoundException(string message = "Not found", Exception? inner = null) : HttpException(404, message, inner);

/// <summary>409 — the request conflicts with the current state, e.g. a duplicate.</summary>
public class ConflictException(string message, Exception? inner = null) : HttpException(409, message, inner);

/// <summary>422 — understood and well-formed, but a rule says no.</summary>
public class UnprocessableEntityException(string message, Exception? inner = null) : HttpException(422, message, inner);

/// <summary>429 — too many requests.</summary>
public class TooManyRequestsException(string message = "Too many requests", Exception? inner = null) : HttpException(429, message, inner);

/// <summary>
///     501 — a route that exists but is not built yet. Named Http-first because an unprefixed
///     NotImplementedException is ambiguous with <see cref="System.NotImplementedException" /> under
///     implicit usings, which made any file importing this namespace fail to compile.
/// </summary>
public class HttpNotImplementedException(string message = "Not implemented", Exception? inner = null) : HttpException(501, message, inner);
