using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Writes a constant, a parameter's default or an enum argument, as C# source that compiles back to the same value.</summary>
public static class DefaultValueReader
{
    /// <summary>The source expression of a parameter default of <paramref name="type" />, <c>default</c> or <c>null</c> when the value is null.</summary>
    public static string ReadExpression(ITypeSymbol type, object? value)
    {
        var valueType = type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? ((INamedTypeSymbol)type).TypeArguments[0]
            : type;

        if (value == null)
        {
            return type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T ? "default" : "null";
        }

        if (valueType.TypeKind == TypeKind.Enum)
        {
            return EnumExpression((INamedTypeSymbol)valueType, value);
        }

        return LiteralExpression(value);
    }

    /// <summary>The enum member holding <paramref name="value" />, or a cast of the number when no member does.</summary>
    internal static string EnumExpression(INamedTypeSymbol enumType, object value)
    {
        var enumMember = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, value));
        var enumTypeName = enumType.ToDisplayString(SymbolDisplayFormats.FullyQualified);
        if (enumMember != null)
        {
            return enumTypeName + "." + enumMember.Name;
        }

        return "(" + enumTypeName + ")" + LiteralExpression(value);
    }

    private static string LiteralExpression(object value)
    {
        var literal = SymbolDisplay.FormatPrimitive(value, true, false);
        // FormatPrimitive drops the suffixes, and without them a default of 1.5 on a decimal does not compile.
        if (value is decimal)
        {
            return literal + "m";
        }

        if (value is float)
        {
            return literal + "f";
        }

        if (value is long)
        {
            return literal + "L";
        }

        if (value is ulong)
        {
            return literal + "UL";
        }

        if (value is uint)
        {
            return literal + "U";
        }

        return literal;
    }
}
