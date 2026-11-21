using MagicCSharp.Generators.Declarations.Dals;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>Reads a filter type's public readable instance properties, own and inherited, into <see cref="FilterField" />s.</summary>
public static class FilterFieldReader
{
    public static EquatableArray<FilterField> Read(ITypeSymbol filterType)
    {
        var seenNames = new HashSet<string>();
        var filterFields = new List<FilterField>();
        for (var declaringType = filterType; declaringType != null; declaringType = declaringType.BaseType)
        {
            var properties = declaringType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic && !property.IsIndexer &&
                                   property.GetMethod != null && property.GetMethod.DeclaredAccessibility == Accessibility.Public);
            foreach (var property in properties)
            {
                if (seenNames.Add(property.Name))
                {
                    filterFields.Add(ReadField(property));
                }
            }
        }

        return new EquatableArray<FilterField>(filterFields);
    }

    private static FilterField ReadField(IPropertySymbol property)
    {
        var rangeValueType = FindComparableRangeValueType(property.Type);
        if (rangeValueType != null)
        {
            return Field(property.Name, FilterFieldShape.ComparableRange, rangeValueType);
        }

        var nullableValueType = UnderlyingTypeOfNullable(property.Type);
        if (nullableValueType != null)
        {
            return Field(property.Name, FilterFieldShape.NullableValue, nullableValueType);
        }

        var listElementType = FindListElementType(property.Type);
        if (listElementType != null)
        {
            return Field(property.Name, FilterFieldShape.List, listElementType);
        }

        return new FilterField { Name = property.Name, Shape = FilterFieldShape.Custom, ValueTypeName = null };
    }

    private static FilterField Field(string name, FilterFieldShape shape, ITypeSymbol valueType)
    {
        return new FilterField
        {
            Name = name,
            Shape = shape,
            ValueTypeName = TypeReferenceReader.Read(valueType.WithNullableAnnotation(NullableAnnotation.NotAnnotated)).FullyQualifiedName,
            IsStringValue = valueType.SpecialType == SpecialType.System_String,
            IsBooleanValue = valueType.SpecialType == SpecialType.System_Boolean,
        };
    }

    private static ITypeSymbol? FindComparableRangeValueType(ITypeSymbol type)
    {
        for (var rangeType = type as INamedTypeSymbol; rangeType != null; rangeType = rangeType.BaseType)
        {
            if (DalReader.MetadataNameOf(rangeType.OriginalDefinition) == FilterTypeNames.ComparableRange)
            {
                return rangeType.TypeArguments[0];
            }
        }

        return null;
    }

    private static ITypeSymbol? UnderlyingTypeOfNullable(ITypeSymbol type)
    {
        var namedType = type as INamedTypeSymbol;
        if (namedType == null || namedType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
        {
            return null;
        }

        return namedType.TypeArguments[0];
    }

    // The helpers take IEnumerable<T> of a struct T or of string, so only those element types make a list field.
    private static ITypeSymbol? FindListElementType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return null;
        }

        var enumerable = IsEnumerableOfT(type)
            ? (INamedTypeSymbol)type
            : type.AllInterfaces.FirstOrDefault(IsEnumerableOfT);
        if (enumerable == null)
        {
            return null;
        }

        var elementType = enumerable.TypeArguments[0];
        var isStructElement = elementType.IsValueType && elementType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T &&
                              elementType.TypeKind != TypeKind.TypeParameter;
        if (isStructElement || elementType.SpecialType == SpecialType.System_String)
        {
            return elementType;
        }

        return null;
    }

    private static bool IsEnumerableOfT(ITypeSymbol type)
    {
        var namedType = type as INamedTypeSymbol;
        return namedType != null && namedType.IsGenericType && DalReader.MetadataNameOf(namedType.OriginalDefinition) == FilterTypeNames.Enumerable;
    }
}
