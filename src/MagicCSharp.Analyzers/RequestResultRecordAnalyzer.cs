using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0022: request, result, payload and event types are records.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequestResultRecordAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0022";

    private static readonly string[] ModelSuffixes = { "Request", "Result", "Payload", "Event" };

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Request and result models must be records",
        "'{0}' ends in a model suffix but is a class; declare it as a 'record' instead", "Style", DiagnosticSeverity.Error, true,
        "Request, Result, Payload and Event types are immutable data carried across a boundary, so they are " +
        "declared as records. Abstract and static classes, classes with a base class, and event handlers " +
        "(named after the event they handle) are not models.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var eventHandler = compilationContext.Compilation.GetTypeByMetadataName(KnownTypeNames.EventHandler);
            compilationContext.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, eventHandler), SyntaxKind.ClassDeclaration);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? eventHandler)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var modifiers = classDeclaration.Modifiers;
        if (modifiers.Any(SyntaxKind.AbstractKeyword) || modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        var name = classDeclaration.Identifier.ValueText;
        if (!HasModelSuffix(name))
        {
            return;
        }

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken);
        if (classSymbol == null)
        {
            return;
        }

        var baseType = classSymbol.BaseType;
        if (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            return;
        }

        if (SymbolFacts.Implements(classSymbol, eventHandler))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(), name));
    }

    private static bool HasModelSuffix(string name)
    {
        foreach (var suffix in ModelSuffixes)
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
