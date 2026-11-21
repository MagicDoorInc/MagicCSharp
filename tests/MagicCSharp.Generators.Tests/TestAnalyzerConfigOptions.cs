using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Generators.Tests;

/// <summary>Editorconfig keys compare without regard to case, as Roslyn's do (it lowercases the keys it parses).</summary>
public sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> valuesByKey = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        return valuesByKey.TryGetValue(key, out value);
    }
}
