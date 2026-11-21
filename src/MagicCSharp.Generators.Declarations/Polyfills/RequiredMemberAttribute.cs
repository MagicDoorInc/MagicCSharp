namespace System.Runtime.CompilerServices;

/// <summary>Lets netstandard2.0 code declare <c>required</c> members.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute
{
}
