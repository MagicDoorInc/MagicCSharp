using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0012: a type's name ends in the suffix of what it implements — <c>UseCase</c> or <c>Repository</c>.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuffixDriftAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0012";

    private const string UseCaseSuffix = "UseCase";
    private const string RepositorySuffix = "Repository";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Type name has drifted from what it implements",
        "Type '{0}' implements {1} but its name does not end in '{2}'", "Naming", DiagnosticSeverity.Error, true,
        "The suffix on a type name is a promise about what it implements. A type implementing " +
        "MagicCSharp.UseCases.IMagicUseCase is a use case and ends in 'UseCase'; a type implementing a " +
        "repository interface from this codebase or from MagicCSharp ends in 'Repository'.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var magicUseCase = compilationContext.Compilation.GetTypeByMetadataName(KnownTypeNames.MagicUseCase);
            compilationContext.RegisterSymbolAction(symbolContext => Analyze(symbolContext, magicUseCase), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol? magicUseCase)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var name = type.Name;

        if (SymbolFacts.Implements(type, magicUseCase) && !name.EndsWith(UseCaseSuffix, StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], name, magicUseCase!.Name,
                UseCaseSuffix));
            return;
        }

        if (name.EndsWith(RepositorySuffix, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var contract in type.AllInterfaces)
        {
            var isRepositoryContract = SymbolFacts.IsFirstParty(contract, context.Compilation) && contract.Name.EndsWith(RepositorySuffix, StringComparison.Ordinal);
            if (!isRepositoryContract)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], name, contract.Name,
                RepositorySuffix));
            return;
        }
    }
}
