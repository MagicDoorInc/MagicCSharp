using MagicCSharp.Generators.Declarations.Clients;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Clients;

/// <summary>
///     Writes the client class of every <c>[MagicClient]</c> interface, with one method per member that carries a
///     client route attribute, and the <c>Add{Client}</c> method that registers it through the client convention.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MagicClientGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(ClientPipelines.MagicClients(context), ClientGenerationOutput.Write);
    }
}
