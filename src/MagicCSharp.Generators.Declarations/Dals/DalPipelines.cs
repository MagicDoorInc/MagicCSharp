using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>Incremental pipelines over a compilation's Dals, for any generator that works from them.</summary>
public static class DalPipelines
{
    /// <summary>
    ///     One <see cref="DalReadResult" /> per <c>[GenerateDalMapping]</c> class: the declaration, or the diagnostics
    ///     of a class whose shape gets no mapping. A class without the attribute is never read, partial or not.
    /// </summary>
    public static IncrementalValuesProvider<DalReadResult> Dals(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.ForAttributeWithMetadataName(DataTypeNames.GenerateDalMappingAttribute, IsClass, ReadDal)
            .Where(readResult => readResult.Declaration != null || readResult.Diagnostics.Count > 0);
    }

    private static bool IsClass(SyntaxNode node, CancellationToken cancellationToken)
    {
        return node is ClassDeclarationSyntax;
    }

    private static DalReadResult ReadDal(GeneratorAttributeSyntaxContext attributeContext, CancellationToken cancellationToken)
    {
        return DalReader.Read((INamedTypeSymbol)attributeContext.TargetSymbol, cancellationToken);
    }
}
