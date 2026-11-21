using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Generators.Tests;

/// <summary>Stands in for MSBuild: the global options are the build properties a project passes to generators.</summary>
public sealed class TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalValues) : AnalyzerConfigOptionsProvider
{
    private static readonly TestAnalyzerConfigOptions NoOptions = new TestAnalyzerConfigOptions(new Dictionary<string, string>());

    public override AnalyzerConfigOptions GlobalOptions { get; } = new TestAnalyzerConfigOptions(globalValues);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return NoOptions;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return NoOptions;
    }
}
