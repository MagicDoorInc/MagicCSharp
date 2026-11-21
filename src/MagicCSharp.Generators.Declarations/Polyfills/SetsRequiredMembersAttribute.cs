namespace System.Diagnostics.CodeAnalysis;

/// <summary>Lets netstandard2.0 code declare <c>required</c> members.</summary>
[AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
internal sealed class SetsRequiredMembersAttribute : Attribute
{
}
