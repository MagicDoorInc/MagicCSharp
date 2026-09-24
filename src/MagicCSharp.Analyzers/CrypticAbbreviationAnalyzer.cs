using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0010: identifiers spell their words out.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CrypticAbbreviationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0010";

    private static readonly HashSet<string> BannedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "repo", "ctx", "mgr", "cfg", "svc", "req", "res", "resp", "msg", "addr",
        "qty", "num", "tmp", "idx", "arr", "obj", "val", "usr", "acct", "txn",
    };

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Identifier uses a cryptic abbreviation",
        "Identifier '{0}' contains the abbreviation '{1}'; spell the word out in full", "Naming", DiagnosticSeverity.Error, true,
        "Abbreviations like 'repo', 'ctx' or 'tmp' force every reader to expand them. Spell the word out " +
        "(repository, context, temporary). Members marked [Obsolete] and members whose name " +
        "is dictated by an override or interface are skipped, and so are parameters of AI tool methods (marked " +
        "[Description]), whose names are the tool's JSON schema.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Parameter, SymbolKind.Field, SymbolKind.Property,
            SymbolKind.Method, SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.VariableDeclarator);
    }

    private static void AnalyzeLocal(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        var local = context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) as ILocalSymbol;
        if (local == null)
        {
            return;
        }

        var diagnostic = CreateDiagnostic(local);
        if (diagnostic != null)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;
        if (SymbolFacts.IsLambdaParameter(symbol) || SymbolFacts.IsObsolete(symbol) || SymbolFacts.IsAiToolParameter(symbol))
        {
            return;
        }

        var method = symbol as IMethodSymbol;
        if (method != null && !IsCheckableMethod(method))
        {
            return;
        }

        var isParameter = symbol.Kind == SymbolKind.Parameter;
        if (!isParameter && SymbolFacts.IsInheritedMember(symbol))
        {
            return;
        }

        var diagnostic = CreateDiagnostic(symbol);
        if (diagnostic != null)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static Diagnostic? CreateDiagnostic(ISymbol symbol)
    {
        var name = symbol.Name;
        if (name.Length <= 1 || symbol.Locations.Length == 0 || !symbol.Locations[0].IsInSource)
        {
            return null;
        }

        var bannedWord = FindBannedWord(name);
        if (bannedWord == null)
        {
            return null;
        }

        return Diagnostic.Create(Rule, symbol.Locations[0], name, bannedWord);
    }

    private static bool IsCheckableMethod(IMethodSymbol method)
    {
        return method.MethodKind == MethodKind.Ordinary || method.MethodKind == MethodKind.LocalFunction;
    }

    private static string? FindBannedWord(string identifier)
    {
        foreach (var word in IdentifierWords.Split(identifier))
        {
            if (BannedWords.Contains(word))
            {
                return word;
            }
        }

        return null;
    }
}
