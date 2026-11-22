using MagicCSharp.Generators.Declarations.Dals;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     Reads a <c>[GenerateFilterQuery]</c> class into a <see cref="FilteredRepositoryDeclaration" />. The class must be
///     partial, concrete, and inherit an abstract <c>ApplyFilter(IQueryable&lt;TDal&gt; query, TFilter filter)</c> over a
///     Dal implementing MagicCSharp's <c>IDalTransform&lt;TEntity, TEdit&gt;</c>; anything else is refused. Recognised by
///     that shape, not by a base class, so a codebase's own repository bases qualify as MagicCSharp's do. A repository
///     that writes <c>ApplyFilter</c> itself, or whose base does, is left alone.
/// </summary>
public static class FilteredRepositoryReader
{
    private const string ApplyFilterName = "ApplyFilter";

    public static FilteredRepositoryReadResult Read(INamedTypeSymbol repositoryClass, CancellationToken cancellationToken)
    {
        if (!TypeDeclarationReader.IsPartial(repositoryClass, cancellationToken))
        {
            return Refused(FilterDiagnostics.FilteredRepositoryNotPartial, repositoryClass);
        }

        if (repositoryClass.TypeKind != TypeKind.Class || repositoryClass.IsAbstract || repositoryClass.IsStatic)
        {
            return Refused(FilterDiagnostics.NotAFilteredRepository, repositoryClass);
        }

        var applyFilter = FindNearestApplyFilter(repositoryClass);
        if (applyFilter == null && TypeDeclarationReader.HasUnresolvedBaseOrInterface(repositoryClass))
        {
            return FilteredRepositoryReadResult.NoApplyFilterToWrite;
        }

        if (applyFilter == null)
        {
            return Refused(FilterDiagnostics.NotAFilteredRepository, repositoryClass);
        }

        if (!applyFilter.IsAbstract)
        {
            return FilteredRepositoryReadResult.NoApplyFilterToWrite;
        }

        var dalType = ((INamedTypeSymbol)applyFilter.ReturnType).TypeArguments[0];
        var filterType = applyFilter.Parameters[1].Type;
        var dalTransform = dalType.AllInterfaces.FirstOrDefault(implemented =>
            DalReader.MetadataNameOf(implemented.OriginalDefinition) == DataTypeNames.DalTransform);
        if (filterType.TypeKind == TypeKind.Error || dalType.TypeKind == TypeKind.Error ||
            (dalTransform != null && dalTransform.TypeArguments[0].TypeKind == TypeKind.Error))
        {
            return FilteredRepositoryReadResult.NoApplyFilterToWrite;
        }

        if (dalTransform == null)
        {
            return Refused(FilterDiagnostics.NotAFilteredRepository, repositoryClass);
        }

        var repository = TypeDeclarationReader.Read(repositoryClass, cancellationToken);
        if (repository.IsNested || repository.TypeParameters.Count > 0)
        {
            var shapeDescriptor = repository.IsNested ? FilterDiagnostics.NestedFilteredRepository : FilterDiagnostics.GenericFilteredRepository;
            return Refused(shapeDescriptor, repositoryClass);
        }

        var entityType = dalTransform.TypeArguments[0];
        return new FilteredRepositoryReadResult
        {
            Declaration = new FilteredRepositoryDeclaration
            {
                Repository = repository,
                Dal = TypeReferenceReader.Read(dalType),
                Entity = TypeReferenceReader.Read(entityType),
                Filter = TypeReferenceReader.Read(filterType.WithNullableAnnotation(NullableAnnotation.NotAnnotated)),
                ApplyFilterAccessibility = OverrideAccessibilityOf(applyFilter, repositoryClass),
                FilterFields = FilterFieldReader.Read(filterType),
                EntityProperties = MappablePropertyReader.Read(entityType),
                DalProperties = MappablePropertyReader.Read(dalType),
                ApplyCustomFilter = CustomFilterMethodReader.ReadApplyCustomFilter(repositoryClass, cancellationToken),
            },
            Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
        };
    }

    // The nearest ApplyFilter up the chain decides: abstract there means no class below it wrote one.
    private static IMethodSymbol? FindNearestApplyFilter(INamedTypeSymbol repositoryClass)
    {
        for (INamedTypeSymbol? declaringType = repositoryClass; declaringType != null; declaringType = declaringType.BaseType)
        {
            var applyFilter = declaringType.GetMembers(ApplyFilterName).OfType<IMethodSymbol>().FirstOrDefault(HasApplyFilterShape);
            if (applyFilter != null)
            {
                return applyFilter;
            }
        }

        return null;
    }

    private static bool HasApplyFilterShape(IMethodSymbol method)
    {
        if (method.IsStatic || method.Parameters.Length != 2 || method.TypeParameters.Length > 0)
        {
            return false;
        }

        var returnType = method.ReturnType as INamedTypeSymbol;
        return returnType != null && DalReader.MetadataNameOf(returnType.OriginalDefinition) == FilterTypeNames.Queryable &&
               SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, returnType);
    }

    // An override repeats the base's accessibility, except that 'protected internal' from another assembly is overridden
    // as 'protected' (the compiler refuses anything else).
    private static string OverrideAccessibilityOf(IMethodSymbol applyFilter, INamedTypeSymbol repositoryClass)
    {
        var isBaseInAnotherAssembly = !SymbolEqualityComparer.Default.Equals(applyFilter.ContainingAssembly, repositoryClass.ContainingAssembly);
        switch (applyFilter.DeclaredAccessibility)
        {
            case Accessibility.Public:
                return "public";
            case Accessibility.Internal:
                return "internal";
            case Accessibility.ProtectedOrInternal:
                return isBaseInAnotherAssembly ? "protected" : "protected internal";
            case Accessibility.ProtectedAndInternal:
                return "private protected";
            default:
                return "protected";
        }
    }

    private static FilteredRepositoryReadResult Refused(DiagnosticDescriptor descriptor, INamedTypeSymbol repositoryClass)
    {
        return new FilteredRepositoryReadResult
        {
            Declaration = null,
            Diagnostics = new EquatableArray<DiagnosticInfo>(
            [
                new DiagnosticInfo
                {
                    Descriptor = descriptor,
                    Location = SourceLocation.From(repositoryClass.Locations.FirstOrDefault()),
                    MessageArguments = new EquatableArray<string>([repositoryClass.Name]),
                },
            ]),
        };
    }
}
