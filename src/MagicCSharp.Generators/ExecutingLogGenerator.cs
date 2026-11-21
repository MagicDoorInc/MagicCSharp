using MagicCSharp.Generators.Declarations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators;

/// <summary>
///     Writes the decorator of every <c>[LogExecuting]</c> use case: it implements the use case's interface, writes
///     the <c>Executing:</c> trace line with the call's arguments and calls the use case, and use case discovery
///     registers it in the class's place. Read-only use cases, a parameter that looks like a secret and a
///     hand-written line left in are refused.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ExecutingLogGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classReadings = context.SyntaxProvider.ForAttributeWithMetadataName(ExecutingLogReader.LogExecutingAttributeMetadataName,
            (node, _) => node is ClassDeclarationSyntax || node is RecordDeclarationSyntax,
            (attributeContext, cancellationToken) => ExecutingLogReader.Read((INamedTypeSymbol)attributeContext.TargetSymbol, cancellationToken));
        var settings = context.AnalyzerConfigOptionsProvider.Select((options, _) => ExecutingLogSettings.Read(options));
        var generatedInterfaceIndex = UseCasePipelines.GeneratedInterfaceIndex(context);

        var decoratorResults = classReadings.Combine(settings)
            .Combine(generatedInterfaceIndex)
            .Select((readingSettingsAndIndex, _) => ExecutingLogDecoratorPlanner.Plan(readingSettingsAndIndex.Left.Left, readingSettingsAndIndex.Left.Right,
                readingSettingsAndIndex.Right));
        context.RegisterSourceOutput(decoratorResults, WriteDecorator);
    }

    private static void WriteDecorator(SourceProductionContext sourceProductionContext, ExecutingLogDecoratorResult decoratorResult)
    {
        foreach (var diagnosticInfo in decoratorResult.Diagnostics)
        {
            sourceProductionContext.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
        }

        if (decoratorResult.File == null)
        {
            return;
        }

        sourceProductionContext.AddSource(decoratorResult.File.HintName, decoratorResult.File.Text);
    }
}
