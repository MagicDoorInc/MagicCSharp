using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Generators.Declarations.ContextFactories;

/// <summary>
///     Which class generated design-time factories derive from: the MSBuild property
///     <c>MagicCSharpDesignTimeContextFactoryBase</c>, a metadata name, or MagicCSharp.Data.Postgres's
///     <c>MagicDbContextFactory&lt;TContext&gt;</c>. A codebase whose factory has other defaults names its own.
/// </summary>
public static class ContextFactoryBaseSetting
{
    public const string PropertyName = "MagicCSharpDesignTimeContextFactoryBase";

    public const string DefaultMetadataName = "MagicCSharp.Data.Postgres.MagicDbContextFactory`1";

    public static string Read(AnalyzerConfigOptionsProvider options)
    {
        if (options.GlobalOptions.TryGetValue("build_property." + PropertyName, out var configuredName) && !string.IsNullOrWhiteSpace(configuredName))
        {
            return configuredName.Trim();
        }

        return DefaultMetadataName;
    }
}
