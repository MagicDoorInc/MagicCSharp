using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Reads a type symbol into the <see cref="TypeReference" /> that names it.</summary>
public static class TypeReferenceReader
{
    public static TypeReference Read(ITypeSymbol type)
    {
        var arrayType = type as IArrayTypeSymbol;
        var namedType = type as INamedTypeSymbol;
        var typeArguments = namedType == null ? Enumerable.Empty<ITypeSymbol>() : (IEnumerable<ITypeSymbol>)namedType.TypeArguments;
        return new TypeReference
        {
            Name = arrayType == null ? type.Name : arrayType.ElementType.Name + "[]",
            Namespace = NamespaceOf(type),
            FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullableAnnotation),
            Kind = KindOf(type),
            IsNullable = type.NullableAnnotation == NullableAnnotation.Annotated ||
                         type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T,
            IsValueType = type.IsValueType,
            IsUnresolved = type.TypeKind == TypeKind.Error,
            TypeArguments = new EquatableArray<TypeReference>(typeArguments.Select(Read)),
            ElementType = arrayType == null ? null : Read(arrayType.ElementType),
        };
    }

    /// <summary>A reference to a type the compilation could not resolve, such as a generated one or a missing attribute class.</summary>
    public static TypeReference Unresolved(string name)
    {
        return new TypeReference
        {
            Name = name,
            Namespace = "",
            FullyQualifiedName = name,
            Kind = TypeReferenceKind.Unresolved,
            IsNullable = false,
            IsValueType = false,
            IsUnresolved = true,
            TypeArguments = EquatableArray<TypeReference>.Empty,
            ElementType = null,
        };
    }

    internal static string NamespaceOf(ITypeSymbol type)
    {
        var containingNamespace = type.ContainingNamespace;
        if (containingNamespace == null || containingNamespace.IsGlobalNamespace)
        {
            return "";
        }

        return containingNamespace.ToDisplayString();
    }

    internal static TypeReferenceKind KindOf(ITypeSymbol type)
    {
        switch (type.TypeKind)
        {
            case TypeKind.Class:
                return type.IsRecord ? TypeReferenceKind.Record : TypeReferenceKind.Class;
            case TypeKind.Struct:
                return type.IsRecord ? TypeReferenceKind.RecordStruct : TypeReferenceKind.Struct;
            case TypeKind.Interface:
                return TypeReferenceKind.Interface;
            case TypeKind.Enum:
                return TypeReferenceKind.Enum;
            case TypeKind.Array:
                return TypeReferenceKind.Array;
            case TypeKind.TypeParameter:
                return TypeReferenceKind.TypeParameter;
            case TypeKind.Error:
                return TypeReferenceKind.Unresolved;
            default:
                return TypeReferenceKind.Other;
        }
    }
}
