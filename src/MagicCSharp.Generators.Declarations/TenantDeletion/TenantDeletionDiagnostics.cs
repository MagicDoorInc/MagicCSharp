using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.TenantDeletion;

/// <summary>The tenant-deletion marker's diagnostics, <c>MCS1601</c>.</summary>
public static class TenantDeletionDiagnostics
{
    public static readonly DiagnosticDescriptor KeptWithoutReason = new DiagnosticDescriptor("MCS1601",
        "[KeptOnTenantDeletion] needs a reason",
        "'{0}' is [KeptOnTenantDeletion] without a reason; say why its rows outlive their tenant",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);
}
