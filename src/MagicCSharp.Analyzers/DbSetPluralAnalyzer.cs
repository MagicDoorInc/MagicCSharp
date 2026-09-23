using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0021: a <c>DbSet</c> property is named in the plural.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DbSetPluralAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0021";

    private static readonly string[] IrregularPlurals = { "People", "Children", "Men", "Women", "Data", "Media", "Criteria" };

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "DbSet property names are plural",
        "DbSet property '{0}' must be plural", "Naming", DiagnosticSeverity.Error, true,
        "A DbSet holds many rows, so its property is named in the plural, like the table it maps.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var dbSet = compilationContext.Compilation.GetTypeByMetadataName(KnownTypeNames.DbSet);
            if (dbSet == null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(symbolContext => Analyze(symbolContext, dbSet), SymbolKind.Property);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol dbSet)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (!SymbolEqualityComparer.Default.Equals(property.Type.OriginalDefinition, dbSet))
        {
            return;
        }

        if (IsPlural(property.Name) || SymbolFacts.IsInheritedMember(property))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, property.Locations[0], property.Name));
    }

    private static bool IsPlural(string name)
    {
        if (name.EndsWith("s", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var irregularPlural in IrregularPlurals)
        {
            if (name.EndsWith(irregularPlural, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
