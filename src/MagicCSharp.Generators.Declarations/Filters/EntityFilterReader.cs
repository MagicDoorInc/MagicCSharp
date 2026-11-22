using MagicCSharp.Generators.Declarations.Dals;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     Reads a <c>[GenerateMatches]</c> class or record into an <see cref="EntityFilterDeclaration" />. It must be
///     partial and list MagicCSharp's <c>IEntityFilter&lt;TEntity&gt;</c>; anything else is refused. A filter that writes
///     <c>Matches(TEntity)</c> itself, or whose base does, is left alone.
/// </summary>
public static class EntityFilterReader
{
    private const string MatchesName = "Matches";

    public static EntityFilterReadResult Read(INamedTypeSymbol filterClass, CancellationToken cancellationToken)
    {
        if (!TypeDeclarationReader.IsPartial(filterClass, cancellationToken))
        {
            return Refused(FilterDiagnostics.EntityFilterNotPartial, filterClass);
        }

        var entityFilter = filterClass.AllInterfaces.FirstOrDefault(implemented =>
            DalReader.MetadataNameOf(implemented.OriginalDefinition) == FilterTypeNames.EntityFilter);
        if (entityFilter == null || filterClass.TypeKind != TypeKind.Class || filterClass.IsStatic)
        {
            return Refused(FilterDiagnostics.NotAnEntityFilter, filterClass);
        }

        var entityType = entityFilter.TypeArguments[0];
        if (entityType.TypeKind == TypeKind.Error || HasWrittenMatches(filterClass, entityType))
        {
            return EntityFilterReadResult.NoMatchesToWrite;
        }

        var filter = TypeDeclarationReader.Read(filterClass, cancellationToken);
        if (filter.IsNested || filter.TypeParameters.Count > 0)
        {
            var shapeDescriptor = filter.IsNested ? FilterDiagnostics.NestedEntityFilter : FilterDiagnostics.GenericEntityFilter;
            return Refused(shapeDescriptor, filterClass);
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

    private static EntityFilterReadResult Refused(DiagnosticDescriptor descriptor, INamedTypeSymbol filterClass)
    {
        return new EntityFilterReadResult
        {
            Declaration = null,
            Diagnostics = new EquatableArray<DiagnosticInfo>(
            [
                new DiagnosticInfo
                {
                    Descriptor = descriptor,
                    Location = SourceLocation.From(filterClass.Locations.FirstOrDefault()),
                    MessageArguments = new EquatableArray<string>([filterClass.Name]),
                },
            ]),
        };
    }
}
