using MagicCSharp.Generators.Declarations.Filters;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators;

/// <summary>
///     Writes <c>Matches</c> for every partial filter that lists <c>IEntityFilter&lt;TEntity&gt;</c> and does not write
///     it, from the same name rules as the repository's generated query (see <see cref="FilterRulePlanner" />), so an
///     in-memory test repository selects what the database selects.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class EntityFilterMatchesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(FilterPipelines.EntityFilters(context), WriteMatches);
    }

    private static void WriteMatches(SourceProductionContext sourceProductionContext, EntityFilterReadResult readResult)
    {
        foreach (var diagnosticInfo in readResult.Diagnostics)
        {
            sourceProductionContext.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
        }

        if (readResult.Declaration == null)
        {
            return;
        }

        var filterMethodPlan = EntityFilterMatchesPlanner.Plan(readResult.Declaration);
        foreach (var failedDiagnosticInfo in filterMethodPlan.Failures)
        {
            sourceProductionContext.ReportDiagnostic(failedDiagnosticInfo.ToDiagnostic());
        }

        var generatedFile = EntityFilterMatchesWriter.WriteFile(readResult.Declaration, filterMethodPlan);
        sourceProductionContext.AddSource(generatedFile.HintName, generatedFile.Text);
    }
}
