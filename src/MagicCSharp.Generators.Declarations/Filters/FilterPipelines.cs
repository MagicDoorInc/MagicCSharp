using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     Incremental pipelines over a compilation's <c>[GenerateFilterQuery]</c> repositories and <c>[GenerateMatches]</c>
///     filters. A class without the attribute is never read, partial or not.
/// </summary>
public static class FilterPipelines
{
    /// <summary>One read result per <c>[GenerateFilterQuery]</c> class that gets a generated <c>ApplyFilter</c>, or is refused one.</summary>
    public static IncrementalValuesProvider<FilteredRepositoryReadResult> FilteredRepositories(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.ForAttributeWithMetadataName(FilterTypeNames.GenerateFilterQueryAttribute, IsClassOrRecord, ReadRepository)
            .Where(readResult => readResult.Declaration != null || readResult.Diagnostics.Count > 0);
    }

    /// <summary>One read result per <c>[GenerateMatches]</c> class or record that gets a generated <c>Matches</c>, or is refused one.</summary>
    public static IncrementalValuesProvider<EntityFilterReadResult> EntityFilters(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.ForAttributeWithMetadataName(FilterTypeNames.GenerateMatchesAttribute, IsClassOrRecord, ReadFilter)
            .Where(readResult => readResult.Declaration != null || readResult.Diagnostics.Count > 0);
    }

    private static bool IsClassOrRecord(SyntaxNode node, CancellationToken cancellationToken)
    {
        return node is ClassDeclarationSyntax || node is RecordDeclarationSyntax;
    }

    private static FilteredRepositoryReadResult ReadRepository(GeneratorAttributeSyntaxContext attributeContext, CancellationToken cancellationToken)
    {
        return FilteredRepositoryReader.Read((INamedTypeSymbol)attributeContext.TargetSymbol, cancellationToken);
    }

    private static EntityFilterReadResult ReadFilter(GeneratorAttributeSyntaxContext attributeContext, CancellationToken cancellationToken)
    {
        return EntityFilterReader.Read((INamedTypeSymbol)attributeContext.TargetSymbol, cancellationToken);
    }
}
