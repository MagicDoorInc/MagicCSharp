using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0008: the current time comes from an injected <c>TimeProvider</c>, never <c>DateTime.Now</c>.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AmbientClockAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0008";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Do not read the ambient clock directly",
        "Replace '{0}' with an injected TimeProvider and call 'timeProvider.GetUtcNow()'", "Design", DiagnosticSeverity.Error, true,
        "DateTime.Now, DateTime.UtcNow, DateTime.Today, DateTimeOffset.Now and DateTimeOffset.UtcNow cannot be " + // conventions: allow — this rule's help text names what it bans
        "controlled by tests or time travel. Read the current time from an injected System.TimeProvider; only a " +
        "class deriving from TimeProvider may read the system clock.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var timeProvider = compilationContext.Compilation.GetTypeByMetadataName(KnownTypeNames.TimeProvider);
            compilationContext.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, timeProvider), SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? timeProvider)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var propertyName = memberAccess.Name.Identifier.ValueText;
        if (propertyName != "Now" && propertyName != "UtcNow" && propertyName != "Today")
        {
            return;
        }

        var property = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol as IPropertySymbol;
        if (property == null)
        {
            return;
        }

        var specialType = property.ContainingType.SpecialType;
        var isSystemClockType = specialType == SpecialType.System_DateTime || property.ContainingType.ToDisplayString() == "System.DateTimeOffset";
        if (!isSystemClockType)
        {
            return;
        }

        if (IsInsideTimeProvider(context.ContainingSymbol, timeProvider))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(), property.ContainingType.Name + "." + propertyName));
    }

    private static bool IsInsideTimeProvider(ISymbol? containingSymbol, INamedTypeSymbol? timeProvider)
    {
        if (containingSymbol == null)
        {
            return false;
        }

        var containingType = containingSymbol as INamedTypeSymbol ?? containingSymbol.ContainingType;
        while (containingType != null)
        {
            if (SymbolFacts.DerivesFrom(containingType, timeProvider))
            {
                return true;
            }

            containingType = containingType.ContainingType;
        }

        return false;
    }
}
