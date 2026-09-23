using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0001: a record declares its members in a body, not a positional parameter list.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PositionalRecordAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0001";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId,
        "Record must declare properties in a body, not a positional parameter list",
        "Record '{0}' uses a positional parameter list; declare its members as 'public required T Name {{ get; init; }}' in a body instead", "Style",
        DiagnosticSeverity.Error, true,
        "Positional records make every member order-dependent at the call site, break callers silently when " +
        "members are reordered, and cannot express 'required' or per-member defaults. Declare record members " + "in a body.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.RecordDeclaration, SyntaxKind.RecordStructDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (RecordDeclarationSyntax)context.Node;
        var parameterList = declaration.ParameterList;
        if (parameterList == null || parameterList.Parameters.Count == 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, parameterList.GetLocation(), declaration.Identifier.ValueText));
    }
}
