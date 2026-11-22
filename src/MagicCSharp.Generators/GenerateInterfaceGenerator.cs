using MagicCSharp.Generators.Declarations;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators;

/// <summary>
///     Writes the interface of every <c>[GenerateInterface]</c> use case class: <c>I{Class}</c>, extending the use
///     case marker interface, with the class's public <c>Execute</c> methods, their doc comments and the
///     <c>[NotLogged]</c> marks on their parameters, and the part of the class that implements it.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class GenerateInterfaceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var readResults = UseCasePipelines.GeneratedInterfaceReadResults(context);
        context.RegisterSourceOutput(readResults, ReportProblemsAndAddInterfaceFile);
    }

    private static void ReportProblemsAndAddInterfaceFile(SourceProductionContext sourceProductionContext, UseCaseReadResult readResult)
    {
        foreach (var diagnosticInfo in readResult.Diagnostics)
        {
            sourceProductionContext.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
        }

        if (readResult.Declaration == null)
        {
            return;
        }

        var generatedFile = UseCaseInterfaceWriter.WriteFile(readResult.Declaration);
        sourceProductionContext.AddSource(generatedFile.HintName, generatedFile.Text);
    }
}
