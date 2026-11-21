using MagicCSharp.Generators.Declarations;
using MagicCSharp.Generators.Declarations.TenantDeletion;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators;

/// <summary>
///     Refuses a <c>[KeptOnTenantDeletion]</c> with a blank reason (MCS1601): a table that outlives its tenant says
///     why, where the next reader sees it. Writes no source.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class KeptOnTenantDeletionReasonCheck : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var blankReasons = context.SyntaxProvider.ForAttributeWithMetadataName(KeptOnTenantDeletionReader.AttributeMetadataName,
            (_, _) => true,
            (attributeContext, cancellationToken) =>
                KeptOnTenantDeletionReader.ReadBlankReasons(attributeContext.TargetSymbol, attributeContext.Attributes, cancellationToken));
        context.RegisterSourceOutput(blankReasons, ReportBlankReasons);
    }

    private static void ReportBlankReasons(SourceProductionContext sourceProductionContext, EquatableArray<DiagnosticInfo> blankReasons)
    {
        foreach (var diagnosticInfo in blankReasons)
        {
            sourceProductionContext.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
        }
    }
}
