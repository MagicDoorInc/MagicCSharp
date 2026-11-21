using MagicCSharp.Generators.Declarations.Clients;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Clients;

/// <summary>Reports a client generation result's diagnostics and adds its files.</summary>
internal static class ClientGenerationOutput
{
    public static void Write(SourceProductionContext sourceProductionContext, ClientGenerationResult clientGenerationResult)
    {
        foreach (var diagnosticInfo in clientGenerationResult.Diagnostics)
        {
            sourceProductionContext.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
        }

        foreach (var generatedFile in clientGenerationResult.Files)
        {
            sourceProductionContext.AddSource(generatedFile.HintName, generatedFile.Text);
        }
    }
}
