using MagicCSharp.Generators.Declarations.Clients;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Clients;

/// <summary>
///     Makes every <c>[GenerateFakeBase]</c> class a fake base: it implements the named interface, and each member it
///     does not already implement is <c>public virtual</c> and throws <c>NotScriptedException</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class FakeBaseGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(ClientPipelines.FakeBases(context), ClientGenerationOutput.Write);
    }
}
