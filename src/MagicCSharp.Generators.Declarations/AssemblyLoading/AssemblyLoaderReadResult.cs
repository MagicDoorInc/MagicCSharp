namespace MagicCSharp.Generators.Declarations.AssemblyLoading;

/// <summary>The loader to write, or why none is written.</summary>
public sealed record AssemblyLoaderReadResult
{
    public static readonly AssemblyLoaderReadResult NotOptedIn = new AssemblyLoaderReadResult
    {
        Declaration = null,
        Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
    };

    /// <summary>Null when the project did not opt in, or already declares its own loader.</summary>
    public required AssemblyLoaderDeclaration? Declaration { get; init; }

    public required EquatableArray<DiagnosticInfo> Diagnostics { get; init; }
}
