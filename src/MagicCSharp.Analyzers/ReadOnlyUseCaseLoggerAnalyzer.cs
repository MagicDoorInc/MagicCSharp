using System.Collections.Immutable;
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

    private const string ExecutingPrefix = "Executing:";

    private static readonly string[] ReadOnlyVerbs = { "Get", "List", "Search" };

    private static readonly string[] AreaPrefixes = { "External", "Internal" };

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
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null || !memberAccess.Name.Identifier.ValueText.StartsWith("Log", StringComparison.Ordinal))
        {
            return;
        }

        if (!LogsExecutingBoilerplate(invocation))
        {
            return;
        }

        var containingType = context.ContainingSymbol?.ContainingType;
        while (containingType != null && !SymbolFacts.Implements(containingType, magicUseCase))
        {
            containingType = containingType.ContainingType;
        }

        if (containingType == null || !IsReadOnlyName(containingType.Name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), containingType.Name));
    }

    private static bool LogsExecutingBoilerplate(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var literal = argument.Expression as LiteralExpressionSyntax;
            var isExecutingLiteral = literal != null && literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                                     literal.Token.ValueText.StartsWith(ExecutingPrefix, StringComparison.Ordinal);
            if (isExecutingLiteral)
            {
                return true;
            }

            var interpolated = argument.Expression as InterpolatedStringExpressionSyntax;
            if (interpolated == null || interpolated.Contents.Count == 0)
            {
                continue;
            }

            var firstText = interpolated.Contents[0] as InterpolatedStringTextSyntax;
            if (firstText != null && firstText.TextToken.ValueText.StartsWith(ExecutingPrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReadOnlyName(string typeName)
    {
        var remainder = typeName;
        var hasStrippedPrefix = true;
        while (hasStrippedPrefix)
        {
            hasStrippedPrefix = false;
            foreach (var prefix in AreaPrefixes)
            {
                if (StartsWithWord(remainder, prefix))
                {
                    remainder = remainder.Substring(prefix.Length);
                    hasStrippedPrefix = true;
                }
            }
        }

        foreach (var verb in ReadOnlyVerbs)
        {
            if (StartsWithWord(remainder, verb))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithWord(string value, string word)
    {
        if (!value.StartsWith(word, StringComparison.Ordinal))
        {
            return false;
        }

        return value.Length == word.Length || char.IsUpper(value[word.Length]);
    }
}
