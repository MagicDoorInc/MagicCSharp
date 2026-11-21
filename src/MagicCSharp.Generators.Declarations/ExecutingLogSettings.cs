using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Generators.Declarations;

/// <summary>The build properties the <c>[LogExecuting]</c> decorator depends on, read once per compilation.</summary>
public sealed record ExecutingLogSettings
{
    /// <summary>The use case marker interface's metadata name; only an interface extending it is a use case interface.</summary>
    public required string MarkerInterfaceMetadataName { get; init; }

    /// <summary>Caller prefixes besides <c>External</c> and <c>Internal</c>, for the read-only check.</summary>
    public required EquatableArray<string> CallerPrefixes { get; init; }

    public static ExecutingLogSettings Read(AnalyzerConfigOptionsProvider options)
    {
        return new ExecutingLogSettings
        {
            MarkerInterfaceMetadataName = UseCaseMarkerInterfaceSetting.Read(options),
            CallerPrefixes = UseCaseCallerPrefixesSetting.Read(options),
        };
    }
}
