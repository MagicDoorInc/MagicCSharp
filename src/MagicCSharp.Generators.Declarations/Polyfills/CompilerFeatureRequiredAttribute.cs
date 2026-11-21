namespace System.Runtime.CompilerServices;

/// <summary>Lets netstandard2.0 code declare <c>required</c> members.</summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
{
    public string FeatureName { get; } = featureName;

    public bool IsOptional { get; init; }
}
