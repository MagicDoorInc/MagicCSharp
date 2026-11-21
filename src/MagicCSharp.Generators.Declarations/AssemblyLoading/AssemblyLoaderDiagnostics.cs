using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.AssemblyLoading;

/// <summary>The generated assembly loader's diagnostics, <c>MCS1609</c>.</summary>
public static class AssemblyLoaderDiagnostics
{
    public static readonly DiagnosticDescriptor AssemblyLoaderAlreadyDeclared = new DiagnosticDescriptor("MCS1609",
        "The project already declares the AssemblyLoader the generator writes",
        "'{0}' is declared in this project, and MagicCSharpGenerateAssemblyLoader writes it from the project references; delete the hand-written one",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);
}
