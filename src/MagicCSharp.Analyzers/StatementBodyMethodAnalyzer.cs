using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0009: methods and local functions have block bodies.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StatementBodyMethodAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0009";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Methods and local functions must use a statement block body",
        "'{0}' is expression-bodied; write it with a block body", "Style", DiagnosticSeverity.Error, true,
        "Expression-bodied methods hide control flow and make it harder to add a second statement later. " +
        "Write methods and local functions as statement blocks; expression-bodied properties are fine.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.ExpressionBody == null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, method.ExpressionBody.GetLocation(), method.Identifier.ValueText));
    }

    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        var localFunction = (LocalFunctionStatementSyntax)context.Node;
        if (localFunction.ExpressionBody == null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, localFunction.ExpressionBody.GetLocation(), localFunction.Identifier.ValueText));
    }
}
