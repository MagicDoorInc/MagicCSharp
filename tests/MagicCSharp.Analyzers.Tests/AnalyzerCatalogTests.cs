using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers.Tests;

public class AnalyzerCatalogTests
{
    [Fact]
    public void Every_rule_is_an_enabled_error_with_a_unique_id()
    {
        var descriptors = typeof(PositionalRecordAnalyzer).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<DiagnosticAnalyzerAttribute>() != null)
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .SelectMany(analyzer => analyzer.SupportedDiagnostics.Select(descriptor => new
            {
                AnalyzerName = analyzer.GetType().Name,
                Descriptor = descriptor,
            }))
            .ToArray();

        var ruleIds = descriptors.Select(entry => entry.Descriptor.Id).Distinct().OrderBy(id => id).ToArray();
        // MCS0016 is unused: the backend these rules came from banned static NLog loggers, which MagicCSharp does
        // not use. The gap keeps every other number the same as there.
        var expectedRuleIds = Enumerable.Range(1, 22).Where(number => number != 16).Select(number => $"MCS{number:0000}").ToArray();
        Assert.Equal(expectedRuleIds, ruleIds);

        foreach (var entry in descriptors)
        {
            Assert.Equal(DiagnosticSeverity.Error, entry.Descriptor.DefaultSeverity);
            Assert.True(entry.Descriptor.IsEnabledByDefault, entry.Descriptor.Id);
        }

        var sharedRuleIds = descriptors.GroupBy(entry => entry.Descriptor.Id)
            .Where(group => group.Select(entry => entry.AnalyzerName).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.Empty(sharedRuleIds);
    }
}
