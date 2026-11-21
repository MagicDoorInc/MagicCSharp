using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Incremental pipelines over the <c>[MagicClient]</c> interfaces and <c>[GenerateFakeBase]</c> classes of a compilation.</summary>
public static class ClientPipelines
{
    public const string MagicClientAttributeMetadataName = "MagicCSharp.Clients.MagicClientAttribute";

    public const string GenerateFakeBaseAttributeMetadataName = "MagicCSharp.Testing.GenerateFakeBaseAttribute";

    /// <summary>One result per <c>[MagicClient]</c> interface: its client class and registration, or its refusal.</summary>
    /// <remarks>
    ///     The interface is found by its attribute, then read against the whole compilation, because the hand-written
    ///     part of its client class and the client convention can change in any file.
    /// </remarks>
    public static IncrementalValuesProvider<ClientGenerationResult> MagicClients(IncrementalGeneratorInitializationContext context)
    {
        var clientInterfaceNames = context.SyntaxProvider.ForAttributeWithMetadataName(MagicClientAttributeMetadataName,
            (node, _) => node is InterfaceDeclarationSyntax,
            (attributeContext, _) => MetadataName((INamedTypeSymbol)attributeContext.TargetSymbol));
        var clientConvention = context.CompilationProvider.Select((compilation, cancellationToken) => ClientConventionReader.Read(compilation, cancellationToken));

        return clientInterfaceNames.Combine(clientConvention)
            .Combine(context.CompilationProvider)
            .Select((nameConventionAndCompilation, cancellationToken) => MagicClientReader.Read(nameConventionAndCompilation.Left.Left,
                nameConventionAndCompilation.Left.Right, nameConventionAndCompilation.Right, cancellationToken));
    }

    /// <summary>One result per <c>[GenerateFakeBase]</c> class: its throwing members, or its refusal.</summary>
    /// <remarks>Read against the whole compilation, because the class's other parts and base classes can change in any file.</remarks>
    public static IncrementalValuesProvider<ClientGenerationResult> FakeBases(IncrementalGeneratorInitializationContext context)
    {
        var fakeBaseNames = context.SyntaxProvider.ForAttributeWithMetadataName(GenerateFakeBaseAttributeMetadataName,
            (node, _) => node is ClassDeclarationSyntax,
            (attributeContext, _) => MetadataName((INamedTypeSymbol)attributeContext.TargetSymbol));

        return fakeBaseNames.Combine(context.CompilationProvider)
            .Select((nameAndCompilation, cancellationToken) => FakeBaseReader.Read(nameAndCompilation.Left, nameAndCompilation.Right, cancellationToken));
    }

    private static string MetadataName(INamedTypeSymbol type)
    {
        if (type.ContainingType != null)
        {
            return MetadataName(type.ContainingType) + "+" + type.MetadataName;
        }

        if (type.ContainingNamespace == null || type.ContainingNamespace.IsGlobalNamespace)
        {
            return type.MetadataName;
        }

        return type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;
    }
}
