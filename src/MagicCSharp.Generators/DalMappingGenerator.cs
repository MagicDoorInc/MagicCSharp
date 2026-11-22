using MagicCSharp.Generators.Declarations.Dals;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators;

/// <summary>
///     Writes <c>ToEntity</c> and <c>Apply</c> for every <c>[GenerateDalMapping]</c> Dal that does not write them
///     itself, copying each property to the property of the same name and type (see <see cref="DalMappingPlanner" />),
///     and <c>From</c> for one that writes neither by hand or lists <c>IDalFromEdit</c>. The attribute is the opt-in: a
///     partial Dal without it gets nothing, and one with it that is not partial is refused (MCS1207).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DalMappingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(DalPipelines.Dals(context), WriteMapping);
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
