using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.TenantDeletion;

/// <summary>Reads the <c>[KeptOnTenantDeletion]</c> markers that give no reason.</summary>
public static class KeptOnTenantDeletionReader
{
    public const string AttributeMetadataName = "MagicCSharp.Data.EntityFramework.Dals.KeptOnTenantDeletionAttribute";

    /// <summary>One diagnostic per marker on <paramref name="dal" /> whose reason is blank.</summary>
    public static EquatableArray<DiagnosticInfo> ReadBlankReasons(ISymbol dal, IEnumerable<AttributeData> markers, CancellationToken cancellationToken)
    {
        var diagnostics = new List<DiagnosticInfo>();
        foreach (var marker in markers)
        {
            var reason = marker.ConstructorArguments.Length == 1 ? marker.ConstructorArguments[0].Value as string : null;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                continue;
            }

            diagnostics.Add(new DiagnosticInfo
            {
                Descriptor = TenantDeletionDiagnostics.KeptWithoutReason,
                Location = SourceLocation.From(marker.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()),
                MessageArguments = new EquatableArray<string>([dal.Name]),
            });
        }

        return new EquatableArray<DiagnosticInfo>(diagnostics);
    }
}
