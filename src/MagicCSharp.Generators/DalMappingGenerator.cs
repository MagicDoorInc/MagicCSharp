using MagicCSharp.Generators.Declarations.Dals;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators;

/// <summary>
///     Writes <c>ToEntity</c>, <c>Apply</c> and <c>From</c> for every partial Dal that does not write them itself,
///     copying each property to the property of the same name and type (see <see cref="DalMappingPlanner" />). No
///     attribute: making the Dal partial is the opt-in.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DalMappingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var partialDals = DalPipelines.Dals(context).Where(readResult => readResult.Declaration == null || readResult.Declaration.Dal.IsPartial);
        context.RegisterSourceOutput(partialDals, WriteMapping);
    }

    private static void WriteMapping(SourceProductionContext sourceProductionContext, DalReadResult readResult)
    {
        foreach (var diagnosticInfo in readResult.Diagnostics)
        {
            sourceProductionContext.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
        }

        if (readResult.Declaration == null)
        {
            return;
        }

        var dalMappingPlan = DalMappingPlanner.Plan(readResult.Declaration);
        foreach (var failedDiagnosticInfo in dalMappingPlan.Failures())
        {
            sourceProductionContext.ReportDiagnostic(failedDiagnosticInfo.ToDiagnostic());
        }

        var generatedFile = DalMappingWriter.WriteFile(readResult.Declaration, dalMappingPlan);
        if (generatedFile != null)
        {
            sourceProductionContext.AddSource(generatedFile.HintName, generatedFile.Text);
        }
    }
}
