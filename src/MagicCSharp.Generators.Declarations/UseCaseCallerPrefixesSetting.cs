using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     The caller prefixes a codebase puts before a use case's verb besides <c>External</c> and <c>Internal</c>: the
///     MSBuild property <c>MagicCSharpUseCaseCallerPrefixes</c>, a <c>;</c>-separated list such as
///     <c>Partner;Admin</c>. With them, <c>PartnerGetInvoiceUseCase</c> counts as read-only like
///     <c>GetInvoiceUseCase</c>.
/// </summary>
public static class UseCaseCallerPrefixesSetting
{
    public const string PropertyName = "MagicCSharpUseCaseCallerPrefixes";

    public static EquatableArray<string> Read(AnalyzerConfigOptionsProvider options)
    {
        if (!options.GlobalOptions.TryGetValue("build_property." + PropertyName, out var configuredPrefixes) || string.IsNullOrWhiteSpace(configuredPrefixes))
        {
            return EquatableArray<string>.Empty;
        }

        var callerPrefixes = configuredPrefixes.Split(';', ',')
            .Select(callerPrefix => callerPrefix.Trim())
            .Where(callerPrefix => callerPrefix.Length > 0);
        return new EquatableArray<string>(callerPrefixes);
    }
}
