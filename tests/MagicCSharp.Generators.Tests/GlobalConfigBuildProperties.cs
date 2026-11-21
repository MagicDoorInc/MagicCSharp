using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Tests;

/// <summary>
///     The path MSBuild properties take to a generator: the SDK writes them as <c>build_property.{Name} = {value}</c> lines in a
///     global editorconfig, which Roslyn parses, so a value is cut at a <c>;</c> or <c>#</c> as an editorconfig comment.
/// </summary>
public static class GlobalConfigBuildProperties
{
    private const string BuildPropertyPrefix = "build_property.";

    /// <summary>The build properties a global editorconfig with these property lines gives a generator, by name without <c>build_property.</c>.</summary>
    public static IReadOnlyDictionary<string, string> Read(params string[] propertyLines)
    {
        var configText = "is_global = true\n" + string.Join("\n", propertyLines.Select(propertyLine => BuildPropertyPrefix + propertyLine));
        var analyzerConfig = AnalyzerConfig.Parse(configText, "/obj/Debug/Acme.GeneratedMSBuildEditorConfig.editorconfig");
        var analyzerConfigSet = AnalyzerConfigSet.Create(new List<AnalyzerConfig> { analyzerConfig });
        return analyzerConfigSet.GlobalConfigOptions.AnalyzerOptions
            .Where(option => option.Key.StartsWith(BuildPropertyPrefix, StringComparison.Ordinal))
            .ToDictionary(option => option.Key.Substring(BuildPropertyPrefix.Length), option => option.Value);
    }
}
