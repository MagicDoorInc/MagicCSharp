using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Reads attributes, with their constructor and named arguments, into <see cref="AttributeDeclaration" />.</summary>
public static class AttributeDeclarationReader
{
    public static AttributeDeclaration Read(AttributeData attribute)
    {
        return new AttributeDeclaration
        {
            Type = attribute.AttributeClass == null ? TypeReferenceReader.Unresolved("") : TypeReferenceReader.Read(attribute.AttributeClass),
            ConstructorArguments = new EquatableArray<AttributeArgument>(attribute.ConstructorArguments.Select(ReadArgument)),
            NamedArguments = new EquatableArray<NamedAttributeArgument>(attribute.NamedArguments.Select(namedArgument => new NamedAttributeArgument
            {
                Name = namedArgument.Key,
                Value = ReadArgument(namedArgument.Value),
            })),
        };
    }

    public static EquatableArray<AttributeDeclaration> ReadAll(IEnumerable<AttributeData> attributes)
    {
        return new EquatableArray<AttributeDeclaration>(attributes.Select(Read));
    }

    private static AttributeArgument ReadArgument(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return NewArgument(AttributeArgumentKind.Null, "null", null, null);
        }

        if (constant.Kind == TypedConstantKind.Array)
        {
            return new AttributeArgument
            {
                Kind = AttributeArgumentKind.Array,
                Expression = constant.ToCSharpString(),
                Value = null,
                Type = constant.Type == null ? null : TypeReferenceReader.Read(constant.Type),
                Elements = new EquatableArray<AttributeArgument>(constant.Values.Select(ReadArgument)),
            };
        }

        if (constant.Kind == TypedConstantKind.Type || constant.Value is ITypeSymbol)
        {
            var namedType = TypeReferenceReader.Read((ITypeSymbol)constant.Value!);
            return NewArgument(AttributeArgumentKind.Type, "typeof(" + namedType.FullyQualifiedName + ")", null, namedType);
        }

        var value = Convert.ToString(constant.Value, CultureInfo.InvariantCulture);
        if (constant.Kind == TypedConstantKind.Enum)
        {
            var enumType = (INamedTypeSymbol)constant.Type!;
            return NewArgument(AttributeArgumentKind.Enum, DefaultValueReader.EnumExpression(enumType, constant.Value!), value, TypeReferenceReader.Read(enumType));
        }

        return NewArgument(AttributeArgumentKind.Primitive, constant.ToCSharpString(), value, null);
    }

    private static AttributeArgument NewArgument(AttributeArgumentKind kind, string expression, string? value, TypeReference? type)
    {
        return new AttributeArgument
        {
            Kind = kind,
            Expression = expression,
            Value = value,
            Type = type,
            Elements = EquatableArray<AttributeArgument>.Empty,
        };
    }
}
