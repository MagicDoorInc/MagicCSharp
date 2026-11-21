using MagicCSharp.Clients;
using MagicCSharp.Generators.Clients;
using MagicCSharp.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Generators.Tests;

/// <summary>Runs the client generators over a subject that can see the client and fake attributes and what generated clients call.</summary>
public static class ClientGeneratorRuns
{
    public static MetadataReference[] References()
    {
        return
        [
            MetadataReference.CreateFromFile(typeof(MagicClientAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(NotScriptedException).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IHttpClientFactory).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IConfiguration).Assembly.Location),
        ];
    }

    public static IIncrementalGenerator[] Generators()
    {
        return [new MagicClientGenerator(), new FakeBaseGenerator()];
    }

    public static GeneratorRunResult Run(string source, params MetadataReference[] otherReferences)
    {
        return GeneratorHarness.Run(new GeneratorRunRequest
        {
            SourceFiles = [GeneratorHarness.Subject(source)],
            OtherGenerators = Generators(),
            AdditionalReferences = [.. References(), .. otherReferences],
        });
    }

    /// <summary>The subject's diagnostics, after checking the refused declaration got no generated file.</summary>
    public static string[] Refusals(string source, params MetadataReference[] otherReferences)
    {
        var generatorRunResult = Run(source, otherReferences);
        Assert.Empty(generatorRunResult.GeneratedFiles);
        return generatorRunResult.Diagnostics;
    }
}
