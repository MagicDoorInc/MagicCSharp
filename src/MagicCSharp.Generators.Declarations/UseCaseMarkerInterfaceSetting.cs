using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Which use case marker interface generated interfaces extend: the MSBuild property
///     <c>MagicCSharpUseCaseMarkerInterface</c>, a full metadata name, or MagicCSharp's own <c>IMagicUseCase</c>. A
///     codebase with its own marker sets the property once, so its dependency-injection scan finds the interfaces.
/// </summary>
public static class UseCaseMarkerInterfaceSetting
{
    public const string PropertyName = "MagicCSharpUseCaseMarkerInterface";

    public const string DefaultMetadataName = "MagicCSharp.UseCases.IMagicUseCase";

    public static string Read(AnalyzerConfigOptionsProvider options)
    {
        if (options.GlobalOptions.TryGetValue("build_property." + PropertyName, out var configuredName) && !string.IsNullOrWhiteSpace(configuredName))
        {
            return configuredName.Trim();
        }

        return DefaultMetadataName;
    }
}
