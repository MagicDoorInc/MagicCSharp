using MagicCSharp.Generators.Declarations.Dals;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     Reads a partial class or record that lists MagicCSharp's <c>IEntityFilter&lt;TEntity&gt;</c> and does not write
///     <c>Matches(TEntity)</c> into an <see cref="EntityFilterDeclaration" />.
/// </summary>
public static class EntityFilterReader
{
    private const string MatchesName = "Matches";

    public static EntityFilterReadResult Read(INamedTypeSymbol filterClass, CancellationToken cancellationToken)
    {
        if (filterClass.TypeKind != TypeKind.Class || filterClass.IsStatic)
        {
            return EntityFilterReadResult.NotAFilter;
        }

        var entityFilter = filterClass.AllInterfaces.FirstOrDefault(implemented =>
            DalReader.MetadataNameOf(implemented.OriginalDefinition) == FilterTypeNames.EntityFilter);
        if (entityFilter == null || entityFilter.TypeArguments[0].TypeKind == TypeKind.Error)
        {
            return EntityFilterReadResult.NotAFilter;
        }

        var entityType = entityFilter.TypeArguments[0];
        if (HasWrittenMatches(filterClass, entityType))
        {
            return EntityFilterReadResult.NotAFilter;
        }

        var filter = TypeDeclarationReader.Read(filterClass, cancellationToken);
        if (!filter.IsPartial)
        {
            return EntityFilterReadResult.NotAFilter;
        }

        if (filter.IsNested || filter.TypeParameters.Count > 0)
        {
            var shapeDescriptor = filter.IsNested ? FilterDiagnostics.NestedEntityFilter : FilterDiagnostics.GenericEntityFilter;
            return Refused(shapeDescriptor, filter);
        }

        return new EntityFilterReadResult
        {
            Declaration = new EntityFilterDeclaration
            {
                Filter = filter,
                Entity = TypeReferenceReader.Read(entityType.WithNullableAnnotation(NullableAnnotation.NotAnnotated)),
                FilterFields = FilterFieldReader.Read(filterClass),
                EntityProperties = MappablePropertyReader.Read(entityType),
                MatchesCustomFilter = CustomFilterMethodReader.ReadMatchesCustomFilter(filterClass, cancellationToken),
            },
            Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
        };
    }

    // A Matches the filter or a base class writes is kept, as a hand-written Dal mapping is.
    private static bool HasWrittenMatches(INamedTypeSymbol filterClass, ITypeSymbol entityType)
    {
        for (INamedTypeSymbol? declaringType = filterClass; declaringType != null; declaringType = declaringType.BaseType)
        {
            var hasMatches = declaringType.GetMembers(MatchesName)
                .OfType<IMethodSymbol>()
                .Any(method => !method.IsStatic && !method.IsAbstract && method.Parameters.Length == 1 &&
                               SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, entityType));
            if (hasMatches)
            {
                return true;
            }
        }

        return false;
    }

    private static EntityFilterReadResult Refused(DiagnosticDescriptor descriptor, TypeDeclaration filter)
    {
        return new EntityFilterReadResult
        {
            Declaration = null,
            Diagnostics = new EquatableArray<DiagnosticInfo>(
            [
                new DiagnosticInfo
                {
                    Descriptor = descriptor,
                    Location = filter.Location,
                    MessageArguments = new EquatableArray<string>([filter.Name]),
                },
            ]),
        };
    }
}
