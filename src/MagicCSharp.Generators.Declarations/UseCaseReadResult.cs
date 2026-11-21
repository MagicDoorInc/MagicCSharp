namespace MagicCSharp.Generators.Declarations;

/// <summary>What reading one <c>[GenerateInterface]</c> class found: a declaration to generate, a skip, or a refusal.</summary>
public sealed record UseCaseReadResult
{
    /// <summary>The use case to generate an interface for; null when it is skipped or refused.</summary>
    public required UseCaseDeclaration? Declaration { get; init; }

    /// <summary>The class already implements its own hand-written interface, so nothing is generated or reported.</summary>
    public required bool IsSkipped { get; init; }

    /// <summary>Why the class is refused; empty unless it is.</summary>
    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }
}
