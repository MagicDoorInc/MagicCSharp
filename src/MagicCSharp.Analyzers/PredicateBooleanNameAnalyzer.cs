using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0011: a boolean's name reads as a yes/no question.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PredicateBooleanNameAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0011";

    private static readonly HashSet<string> PredicatePrefixes = new HashSet<string>(StringComparer.Ordinal)
    {
        "is", "has", "can", "should", "allow", "was", "will", "must", "are",
        "Is", "Has", "Can", "Should", "Allow", "Was", "Will", "Must", "Are",
    };

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Boolean identifier does not read as a predicate",
        "Boolean '{0}' does not start with is/has/can/should/allow/was/will/must/are; rename it so it reads as a yes/no question", "Naming",
        DiagnosticSeverity.Error, true,
        "A boolean named 'Active' or 'enabled' reads like a noun or a verb. Prefix it so 'if (lease.IsActive)' " +
        "reads as a question. To rename a property that is part of a contract (a DTO, an event, stored JSON), " +
        "add the new property and keep the old one marked [Obsolete] and forwarding to it; [Obsolete] members " +
        "and members whose name is dictated by an override or interface are skipped, and so are parameters of AI " +
        "tool methods (marked [Description]), whose names are the tool's JSON schema.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Parameter, SymbolKind.Field, SymbolKind.Property);
        context.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.VariableDeclarator);
    }

    private static void AnalyzeLocal(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        var local = context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) as ILocalSymbol;
        if (local == null || !IsMisnamedBoolean(local.Type, local.Name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, local.Locations[0], local.Name));
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;
        if (SymbolFacts.IsLambdaParameter(symbol) || SymbolFacts.IsObsolete(symbol) || SymbolFacts.IsAiToolParameter(symbol) || symbol.IsImplicitlyDeclared)
        {
            return;
        }

        var parameter = symbol as IParameterSymbol;
        if (parameter == null && SymbolFacts.IsInheritedMember(symbol))
        {
            return;
        }

        if (parameter != null && IsAccessorParameter(parameter))
        {
            return;
        }

        var type = TypeOf(symbol);
        if (type == null || !IsMisnamedBoolean(type, symbol.Name))
        {
            return;
        }

        if (symbol.Locations.Length == 0 || !symbol.Locations[0].IsInSource)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, symbol.Locations[0], symbol.Name));
    }

    private static bool IsAccessorParameter(IParameterSymbol parameter)
    {
        var method = parameter.ContainingSymbol as IMethodSymbol;
        if (method == null)
        {
            return false;
        }

        return method.AssociatedSymbol != null;
    }

    private static bool IsMisnamedBoolean(ITypeSymbol type, string name)
    {
        if (type.SpecialType != SpecialType.System_Boolean || name == "_")
        {
            return false;
        }

        if (PredicatePrefixes.Contains(name))
        {
            return false;
        }

        return !PredicatePrefixes.Contains(IdentifierWords.First(name));
    }

    private static ITypeSymbol? TypeOf(ISymbol symbol)
    {
        var parameter = symbol as IParameterSymbol;
        if (parameter != null)
        {
            return parameter.Type;
        }

        var field = symbol as IFieldSymbol;
        if (field != null)
        {
            return field.AssociatedSymbol == null ? field.Type : null;
        }

        var property = symbol as IPropertySymbol;
        return property != null ? property.Type : null;
    }
}
