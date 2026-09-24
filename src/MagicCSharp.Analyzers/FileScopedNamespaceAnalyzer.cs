using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0018: namespaces are file-scoped.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileScopedNamespaceAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0018";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Use a file-scoped namespace",
        "Declare namespace '{0}' file-scoped, as 'namespace {0};'", "Style", DiagnosticSeverity.Error, true,
        "A block-bodied namespace adds an indentation level to every file. Use file-scoped " + "namespace declarations exclusively.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.NamespaceDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var namespaceDeclaration = (NamespaceDeclarationSyntax)context.Node;
        context.ReportDiagnostic(Diagnostic.Create(Rule, namespaceDeclaration.Name.GetLocation(), namespaceDeclaration.Name.ToString()));
    }
}
