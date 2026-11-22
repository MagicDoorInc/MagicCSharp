using MagicCSharp.Generators.Declarations.Filters;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators;

/// <summary>
///     Writes <c>ApplyFilter</c> for every <c>[GenerateFilterQuery]</c> repository that inherits it abstract and does
///     not write it, one query helper call per filter field the name rules match (see <see cref="FilterRulePlanner" />).
///     The attribute is the opt-in: a partial repository without it gets nothing, and one with it that is not partial
///     is refused (MCS1309).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class FilterQueryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(FilterPipelines.FilteredRepositories(context), WriteApplyFilter);
    }

    private static void WriteApplyFilter(SourceProductionContext sourceProductionContext, FilteredRepositoryReadResult readResult)
    {
        foreach (var diagnosticInfo in readResult.Diagnostics)
        {
            sourceProductionContext.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
        }

        if (readResult.Declaration == null)
        {
            return;
        }

        var filterMethodPlan = FilterQueryPlanner.Plan(readResult.Declaration);
        foreach (var failedDiagnosticInfo in filterMethodPlan.Failures)
        {
            sourceProductionContext.ReportDiagnostic(failedDiagnosticInfo.ToDiagnostic());
        }

        var generatedFile = FilterQueryWriter.WriteFile(readResult.Declaration, filterMethodPlan);
        sourceProductionContext.AddSource(generatedFile.HintName, generatedFile.Text);
    }
}
