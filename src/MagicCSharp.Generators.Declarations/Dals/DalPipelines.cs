using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>Incremental pipelines over a compilation's Dals, for any generator that works from them.</summary>
public static class DalPipelines
{
    /// <summary>
    ///     One <see cref="DalReadResult" /> per class implementing <c>IDalTransform&lt;TEntity, TEdit&gt;</c>, partial or
    ///     not: the declaration, or the diagnostics of a partial Dal whose shape gets no mapping.
    /// </summary>
    public static IncrementalValuesProvider<DalReadResult> Dals(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.CreateSyntaxProvider(IsClassWithBaseList, ReadOncePerClass)
            .Where(readResult => readResult.Declaration != null || readResult.Diagnostics.Count > 0);
    }

    private static bool IsClassWithBaseList(SyntaxNode node, CancellationToken cancellationToken)
    {
        var classDeclaration = node as ClassDeclarationSyntax;
        return classDeclaration != null && classDeclaration.BaseList != null;
    }

    // A partial class can list its bases on several parts; only the first such part reads it, so the class gets
    // one generated file rather than one per part.
    private static DalReadResult ReadOncePerClass(GeneratorSyntaxContext syntaxContext, CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)syntaxContext.Node;
        var dalClass = syntaxContext.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;
        if (dalClass == null)
        {
            return DalReader.NotADal;
        }

        var firstPartWithBaseList = dalClass.DeclaringSyntaxReferences.Select(syntaxReference => syntaxReference.GetSyntax(cancellationToken))
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(declaration => declaration.BaseList != null);
        if (firstPartWithBaseList != classDeclaration)
        {
            return DalReader.NotADal;
        }

        return DalReader.Read(dalClass, cancellationToken);
    }
}
