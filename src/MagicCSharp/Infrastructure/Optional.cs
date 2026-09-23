namespace MagicCSharp.Infrastructure;

/// <summary>
///     The non-generic face of <see cref="Optional{T}" />, so serialization can ask whether a value is present
///     without knowing what type it wraps.
/// </summary>
public interface IOptional
{
    /// <summary>Whether a value was supplied.</summary>
    bool HasValue { get; }
}

/// <summary>
///     A value that may not have been supplied at all, as distinct from one supplied as <c>null</c>.
///     <para>
///         This is the type a partial update needs. On an edit record, <c>string? Name</c> cannot tell "leave the
///         name alone" apart from "clear the name" — both arrive as <c>null</c>. <c>Optional&lt;string?&gt; Name</c>
///         can: <see cref="HasValue" /> is false when the caller omitted the property, and true with a null
///         <see cref="Value" /> when the caller sent <c>"name": null</c>.
///     </para>
///     <para>
///         <c>default(Optional&lt;T&gt;)</c> is the absent case, so a property the deserializer never touches is
///         absent without any further work.
///     </para>
/// </summary>
/// <example>
///     <code language="csharp">
///     public record UpdateUserRequest
///     {
///         public Optional&lt;string?&gt; NickName { get; init; }
///     }
///
///     if (request.NickName.HasValue)
///     {
///         user.NickName = request.NickName.Value;
///     }
///     </code>
/// </example>
public readonly struct Optional<T>(T value) : IOptional
{
    /// <summary>
    ///     Whether a value was supplied. False for <c>default(Optional&lt;T&gt;)</c>.
    /// </summary>
    public bool HasValue { get; } = true;

    /// <summary>
    ///     The supplied value. Meaningless unless <see cref="HasValue" /> is true.
    /// </summary>
    public T Value { get; } = value;

    /// <summary>
    ///     Allow a bare value where an optional is expected, so callers write <c>Name = "Barbara"</c>.
    /// </summary>
    public static implicit operator Optional<T>(T value)
    {
        return new Optional<T>(value);
    }

    /// <summary>
    ///     The value when one was supplied, otherwise <paramref name="fallback" />.
    /// </summary>
    public T OrElse(T fallback)
    {
        return HasValue ? Value : fallback;
    }

    public override string ToString()
    {
        return HasValue ? Value?.ToString() ?? "null" : "undefined";
    }
}
