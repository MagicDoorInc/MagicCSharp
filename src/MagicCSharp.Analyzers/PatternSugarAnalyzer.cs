using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0003–MCS0005: null checks use <c>==</c>/<c>!=</c>, and patterns neither read properties nor declare variables.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PatternSugarAnalyzer : DiagnosticAnalyzer
{
    public const string NullPatternDiagnosticId = "MCS0003";
    public const string PropertyPatternDiagnosticId = "MCS0004";
    public const string DeclarationPatternDiagnosticId = "MCS0005";

    private static readonly DiagnosticDescriptor NullPatternRule = new DiagnosticDescriptor(NullPatternDiagnosticId,
        "Use an equality comparison instead of a null pattern", "Replace '{0}' with '{1}'", "Style", DiagnosticSeverity.Error,
        true, "Write null checks as '== null' and '!= null'. A 'null' arm of a switch has no equality " + "form and is allowed.");

    private static readonly DiagnosticDescriptor PropertyPatternRule = new DiagnosticDescriptor(PropertyPatternDiagnosticId,
        "Use explicit checks instead of a property pattern", "Property patterns are not allowed; write the checks out explicitly", "Style",
        DiagnosticSeverity.Error, true,
        "Property patterns combine a null check, member reads and a variable declaration into one " +
        "expression. Write each check on its own so the control flow is readable.");

    private static readonly DiagnosticDescriptor DeclarationPatternRule = new DiagnosticDescriptor(DeclarationPatternDiagnosticId,
        "Use a cast instead of a pattern that declares a variable", "Pattern declares '{0}' inline; use 'as' plus a null check, or an explicit cast", "Style",
        DiagnosticSeverity.Error, true,
        "A pattern that introduces a variable hides both the conversion and the scope of that variable. " +
        "Do the conversion on its own line so both are visible.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(NullPatternRule, PropertyPatternRule, DeclarationPatternRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeConstantPattern, SyntaxKind.ConstantPattern);
        context.RegisterSyntaxNodeAction(AnalyzeRecursivePattern, SyntaxKind.RecursivePattern);
        context.RegisterSyntaxNodeAction(AnalyzeDeclarationPattern, SyntaxKind.DeclarationPattern);
    }

    private static void AnalyzeConstantPattern(SyntaxNodeAnalysisContext context)
    {
        var pattern = (ConstantPatternSyntax)context.Node;
        if (!pattern.Expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return;
        }

        if (!IsInsideIsExpression(pattern))
        {
            return;
        }

        var parent = pattern.Parent;
        var isNegated = parent != null && parent.IsKind(SyntaxKind.NotPattern);
        var reported = isNegated ? parent! : pattern;
        context.ReportDiagnostic(Diagnostic.Create(NullPatternRule, reported.GetLocation(), isNegated ? "is not null" : "is null",
            isNegated ? "!= null" : "== null"));
    }

    private static void AnalyzeRecursivePattern(SyntaxNodeAnalysisContext context)
    {
        var pattern = (RecursivePatternSyntax)context.Node;
        if (pattern.PropertyPatternClause == null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(PropertyPatternRule, pattern.GetLocation()));
    }

    private static void AnalyzeDeclarationPattern(SyntaxNodeAnalysisContext context)
    {
        var pattern = (DeclarationPatternSyntax)context.Node;
        var designation = pattern.Designation as SingleVariableDesignationSyntax;
        if (designation == null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DeclarationPatternRule, pattern.GetLocation(), designation.Identifier.ValueText));
    }

    private static bool IsInsideIsExpression(PatternSyntax pattern)
    {
        var current = pattern.Parent;
        while (current != null && current is PatternSyntax)
        {
            current = current.Parent;
        }

        return current != null && current.IsKind(SyntaxKind.IsPatternExpression);
    }
}
