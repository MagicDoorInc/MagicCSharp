using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0017: no mutable static fields or settable static properties.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutableStaticStateAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0017";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Do not declare mutable static state",
        "'{0}' is a mutable static {1}; make it an instance member, or 'readonly' or 'const' if it never changes", "Reliability", DiagnosticSeverity.Error,
        true,
        "Static mutable fields and settable static properties are shared by every request the process " +
        "handles, which makes them a concurrency hazard. [ThreadStatic] fields are allowed.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        var modifiers = field.Modifiers;
        if (!modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        if (modifiers.Any(SyntaxKind.ConstKeyword) || modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            return;
        }

        foreach (var variable in field.Declaration.Variables)
        {
            var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken);
            if (fieldSymbol != null && IsThreadStatic(fieldSymbol))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, variable.Identifier.GetLocation(), variable.Identifier.ValueText, "field"));
        }
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (!property.Modifiers.Any(SyntaxKind.StaticKeyword) || !HasSetter(property))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, property.Identifier.GetLocation(), property.Identifier.ValueText, "property"));
    }

    private static bool HasSetter(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList == null)
        {
            return false;
        }

        foreach (var accessor in property.AccessorList.Accessors)
        {
            if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration) || accessor.IsKind(SyntaxKind.InitAccessorDeclaration))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsThreadStatic(ISymbol fieldSymbol)
    {
        foreach (var attribute in fieldSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == "System.ThreadStaticAttribute")
            {
                return true;
            }
        }

        return false;
    }
}
