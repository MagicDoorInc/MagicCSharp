namespace System.Diagnostics.CodeAnalysis;

/// <summary>
///     Lets netstandard2.0 code tell callers a <c>TryFind</c> out value is set when it returns the given value. The
///     compiler reads only the type and the constructor argument, so the names are this codebase's.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class NotNullWhenAttribute(bool isNotNullWhenReturning) : Attribute
{
    public bool IsNotNullWhenReturning { get; } = isNotNullWhenReturning;
}
