using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>Incremental pipelines over a compilation's filtered repositories and entity filters.</summary>
public static class FilterPipelines
{
    /// <summary>One read result per partial class that gets a generated <c>ApplyFilter</c>, or is refused one.</summary>
    public static IncrementalValuesProvider<FilteredRepositoryReadResult> FilteredRepositories(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.CreateSyntaxProvider(IsPartialTypeWithBaseList, ReadRepositoryOncePerType)
            .Where(readResult => readResult.Declaration != null || readResult.Diagnostics.Count > 0);
    }

    /// <summary>One read result per partial class or record that gets a generated <c>Matches</c>, or is refused one.</summary>
    public static IncrementalValuesProvider<EntityFilterReadResult> EntityFilters(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.CreateSyntaxProvider(IsPartialTypeWithBaseList, ReadFilterOncePerType)
            .Where(readResult => readResult.Declaration != null || readResult.Diagnostics.Count > 0);
    }

    private static bool IsPartialTypeWithBaseList(SyntaxNode node, CancellationToken cancellationToken)
    {
        var isClassOrRecord = node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.RecordDeclaration);
        var typeDeclaration = node as TypeDeclarationSyntax;
        return isClassOrRecord && typeDeclaration != null && typeDeclaration.BaseList != null &&
               typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static FilteredRepositoryReadResult ReadRepositoryOncePerType(GeneratorSyntaxContext syntaxContext, CancellationToken cancellationToken)
    {
        var type = FirstPartWithBaseList(syntaxContext, cancellationToken);
        return type == null ? FilteredRepositoryReadResult.NotARepository : FilteredRepositoryReader.Read(type, cancellationToken);
    }

    private static EntityFilterReadResult ReadFilterOncePerType(GeneratorSyntaxContext syntaxContext, CancellationToken cancellationToken)
    {
        var type = FirstPartWithBaseList(syntaxContext, cancellationToken);
        return type == null ? EntityFilterReadResult.NotAFilter : EntityFilterReader.Read(type, cancellationToken);
    }

    // A partial type can list its bases on several parts; only the first such part reads it, so the type gets one
    // generated file rather than one per part.
    private static INamedTypeSymbol? FirstPartWithBaseList(GeneratorSyntaxContext syntaxContext, CancellationToken cancellationToken)
    {
        var typeDeclaration = (TypeDeclarationSyntax)syntaxContext.Node;
        var type = syntaxContext.SemanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken) as INamedTypeSymbol;
        if (type == null)
        {
            return null;
        }

        var firstPartWithBaseList = type.DeclaringSyntaxReferences.Select(syntaxReference => syntaxReference.GetSyntax(cancellationToken))
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(declaration => declaration.BaseList != null);
        return firstPartWithBaseList == typeDeclaration ? type : null;
    }
}
