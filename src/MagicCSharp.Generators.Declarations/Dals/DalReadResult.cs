namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>What <see cref="DalReader" /> made of one class.</summary>
public sealed record DalReadResult
{
    /// <summary>The Dal, or null when the class is not one the mapping can read (see <see cref="Diagnostics" />).</summary>
    public required DalDeclaration? Declaration { get; init; }

    /// <summary>Why a <c>[GenerateDalMapping]</c> class that needs generated methods cannot get them.</summary>
    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }
}
