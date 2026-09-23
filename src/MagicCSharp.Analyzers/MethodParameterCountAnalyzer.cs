using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0002: at most four parameters per method; related ones belong in a request record.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MethodParameterCountAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0002";

    public const int MaxParameters = 4;

    private const string DescriptionAttribute = "System.ComponentModel.DescriptionAttribute";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Method takes too many parameters",
        "Method '{0}' takes {1} parameters; the limit is " + MaxParameters + ", so group the related ones into a request type", "Design",
        DiagnosticSeverity.Error, true,
        "A long parameter list hides that several arguments belong together and makes every call site " +
        "order-dependent. Wrap the related parameters in a named request record instead. Controller actions " +
        "and AI tool methods (methods or parameters carrying [Description]) are exempt because each parameter " +
        "is bound from the route, query, body, services or the tool schema the model fills in, and overrides " +
        "and interface implementations are reported at the member that dictates their signature.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var controllerBase = compilationContext.Compilation.GetTypeByMetadataName(KnownTypeNames.ControllerBase);
            compilationContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, controllerBase), SyntaxKind.MethodDeclaration);
            compilationContext.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
        });
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol? controllerBase)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (declaration.ParameterList.Parameters.Count <= MaxParameters)
        {
            return;
        }

        var method = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
        if (method == null || SymbolFacts.IsInheritedMember(method))
        {
            return;
        }

        if (IsControllerAction(method, controllerBase) || IsAiToolMethod(method))
        {
            return;
        }

        Report(context, declaration.ParameterList, declaration.Identifier.ValueText);
    }

    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        var declaration = (LocalFunctionStatementSyntax)context.Node;
        Report(context, declaration.ParameterList, declaration.Identifier.ValueText);
    }

    private static bool IsControllerAction(IMethodSymbol method, INamedTypeSymbol? controllerBase)
    {
        if (method.IsStatic || method.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        return SymbolFacts.DerivesFrom(method.ContainingType, controllerBase);
    }

    private static bool IsAiToolMethod(IMethodSymbol method)
    {
        if (SymbolFacts.HasAttribute(method, DescriptionAttribute))
        {
            return true;
        }

        foreach (var parameter in method.Parameters)
        {
            if (SymbolFacts.HasAttribute(parameter, DescriptionAttribute))
            {
                return true;
            }
        }

        return false;
    }

    private static void Report(SyntaxNodeAnalysisContext context, ParameterListSyntax parameterList, string name)
    {
        var count = parameterList.Parameters.Count;
        if (count <= MaxParameters)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, parameterList.GetLocation(), name, count));
    }
}
