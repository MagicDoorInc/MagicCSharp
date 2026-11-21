using System.Collections.Immutable;
using MagicCSharp.Generators.Declarations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0015: a read-only use case does not log the <c>Executing:</c> line.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadOnlyUseCaseLoggerAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0015";

    private static readonly string[] ReadOnlyVerbs = { "Get", "List", "Search" };

    private static readonly string[] NoExtraCallerPrefixes = { };

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Read-only use case must not log the Executing boilerplate",
        "'{0}' is a read-only use case; remove the \"Executing:\" log line", "Style", DiagnosticSeverity.Error, true,
        "The 'Executing: request={request}' trace line is the convention for side-effect use cases. On a read-only " +
        "use case (Get, List or Search, optionally behind an External or Internal prefix) it " +
        "is noise on every request. Any other logging is allowed.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var magicUseCase = compilationContext.Compilation.GetTypeByMetadataName(KnownTypeNames.MagicUseCase);
            if (magicUseCase == null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, magicUseCase), SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol magicUseCase)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!ExecutingLineSyntax.IsExecutingLine(invocation))
        {
            return;
        }

        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var containingType = context.ContainingSymbol?.ContainingType;
        while (containingType != null && !SymbolFacts.Implements(containingType, magicUseCase))
        {
            containingType = containingType.ContainingType;
        }

        if (containingType == null || !ReadOnlyUseCaseName.IsReadOnly(containingType.Name, ReadOnlyVerbs, NoExtraCallerPrefixes))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), containingType.Name));
    }
}
