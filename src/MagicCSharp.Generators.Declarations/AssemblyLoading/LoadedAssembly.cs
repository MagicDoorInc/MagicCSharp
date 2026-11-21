namespace MagicCSharp.Generators.Declarations.AssemblyLoading;

/// <summary>A referenced project the loader loads, and the public type it touches to do so, if it has one.</summary>
public sealed record LoadedAssembly
{
    public required string AssemblyName { get; init; }

    /// <summary>
    ///     A <c>typeof</c> operand: a <c>global::</c>-qualified public top-level type, unbound (<c>X&lt;,&gt;</c>) when
    ///     generic. Null when the assembly has no public type; it is then loaded by name.
    /// </summary>
    public required string? PublicTypeOperand { get; init; }
}
