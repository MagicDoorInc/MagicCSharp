namespace MagicCSharp.UseCases;

/// <summary>
///     Leaves a parameter out of the <c>Executing:</c> line that <see cref="LogExecutingAttribute" /> writes. A
///     parameter that looks like a secret must carry it, or the build fails.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public sealed class NotLoggedAttribute : Attribute
{
}
