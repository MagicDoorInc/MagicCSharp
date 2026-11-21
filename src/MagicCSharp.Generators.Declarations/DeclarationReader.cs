using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Reads Roslyn symbols into the declaration model, the plain values generators compare and write from.</summary>
public static class DeclarationReader
{
    private static readonly SymbolDisplayFormat FullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat FullyQualifiedWithoutNullableFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    public static TypeDeclaration ReadType(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        return new TypeDeclaration
        {
            Name = type.Name,
            Namespace = NamespaceOf(type),
            FullyQualifiedName = type.ToDisplayString(FullyQualifiedWithoutNullableFormat),
            Kind = KindOf(type),
            Accessibility = type.DeclaredAccessibility,
            IsPartial = IsPartial(type, cancellationToken),
            IsAbstract = type.IsAbstract,
            IsStatic = type.IsStatic,
            IsNested = type.ContainingType != null,
            TypeParameters = new EquatableArray<string>(type.TypeParameters.Select(typeParameter => typeParameter.Name)),
            BaseTypes = new EquatableArray<TypeReference>(BaseTypesOf(type).Select(ReadTypeReference)),
            Interfaces = new EquatableArray<TypeReference>(type.Interfaces.Select(ReadTypeReference)),
            AllInterfaces = new EquatableArray<TypeReference>(type.AllInterfaces.Select(ReadTypeReference)),
            Properties = new EquatableArray<PropertyDeclaration>(PublicInstanceProperties(type).Select(property => ReadProperty(property, cancellationToken))),
            Methods = new EquatableArray<MethodDeclaration>(DeclaredMethods(type).Select(method => ReadMethod(method, cancellationToken))),
            Attributes = ReadAttributes(type.GetAttributes()),
            Documentation = ReadDocumentation(type, cancellationToken),
            Location = SourceLocation.From(type.Locations.FirstOrDefault()),
        };
    }

    public static TypeReference ReadTypeReference(ITypeSymbol type)
    {
        var arrayType = type as IArrayTypeSymbol;
        var namedType = type as INamedTypeSymbol;
        var typeArguments = namedType == null ? Enumerable.Empty<ITypeSymbol>() : (IEnumerable<ITypeSymbol>)namedType.TypeArguments;
        return new TypeReference
        {
            Name = arrayType == null ? type.Name : arrayType.ElementType.Name + "[]",
            Namespace = NamespaceOf(type),
            FullyQualifiedName = type.ToDisplayString(FullyQualifiedFormat),
            Kind = KindOf(type),
            IsNullable = type.NullableAnnotation == NullableAnnotation.Annotated ||
                         type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T,
            IsValueType = type.IsValueType,
            IsUnresolved = type.TypeKind == TypeKind.Error,
            TypeArguments = new EquatableArray<TypeReference>(typeArguments.Select(ReadTypeReference)),
            ElementType = arrayType == null ? null : ReadTypeReference(arrayType.ElementType),
        };
    }

    public static MethodDeclaration ReadMethod(IMethodSymbol method, CancellationToken cancellationToken)
    {
        return new MethodDeclaration
        {
            Name = method.Name,
            ReturnType = ReadTypeReference(method.ReturnType),
            Parameters = new EquatableArray<ParameterDeclaration>(method.Parameters.Select(ReadParameter)),
            TypeParameters = new EquatableArray<string>(method.TypeParameters.Select(typeParameter => typeParameter.Name)),
            Accessibility = method.DeclaredAccessibility,
            IsStatic = method.IsStatic,
            IsOverride = method.IsOverride,
            IsAbstract = method.IsAbstract,
            Documentation = ReadDocumentation(method, cancellationToken),
            Attributes = ReadAttributes(method.GetAttributes()),
            Location = SourceLocation.From(method.Locations.FirstOrDefault()),
        };
    }

    public static PropertyDeclaration ReadProperty(IPropertySymbol property, CancellationToken cancellationToken)
    {
        return new PropertyDeclaration
        {
            Name = property.Name,
            Type = ReadTypeReference(property.Type),
            Accessibility = property.DeclaredAccessibility,
            IsRequired = property.IsRequired,
            HasGetter = property.GetMethod != null,
            SetterKind = SetterKindOf(property),
            DeclaringTypeFullyQualifiedName = property.ContainingType.ToDisplayString(FullyQualifiedWithoutNullableFormat),
            Documentation = ReadDocumentation(property, cancellationToken),
            Attributes = ReadAttributes(property.GetAttributes()),
            Location = SourceLocation.From(property.Locations.FirstOrDefault()),
        };
    }

    public static AttributeDeclaration ReadAttribute(AttributeData attribute)
    {
        return new AttributeDeclaration
        {
            Type = attribute.AttributeClass == null ? UnresolvedType("") : ReadTypeReference(attribute.AttributeClass),
            ConstructorArguments = new EquatableArray<AttributeArgument>(attribute.ConstructorArguments.Select(ReadAttributeArgument)),
            NamedArguments = new EquatableArray<NamedAttributeArgument>(attribute.NamedArguments.Select(namedArgument => new NamedAttributeArgument
            {
                Name = namedArgument.Key,
                Value = ReadAttributeArgument(namedArgument.Value),
            })),
        };
    }

    /// <summary>
    ///     The doc comment of a symbol. In source it is read from the <c>///</c> lines themselves, which are there
    ///     whether or not the project generates a documentation file; from another assembly it comes from that
    ///     assembly's XML documentation file, when the compiler found one.
    /// </summary>
    public static DocumentationComment ReadDocumentation(ISymbol symbol, CancellationToken cancellationToken)
    {
        if (symbol.DeclaringSyntaxReferences.Length > 0)
        {
            return DocumentationFromSource(symbol, cancellationToken);
        }

        return DocumentationFromXml(symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken) ?? "");
    }

    private static DocumentationComment DocumentationFromSource(ISymbol symbol, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            var leadingTrivia = syntaxReference.GetSyntax(cancellationToken).GetLeadingTrivia().ToFullString();
            foreach (var line in leadingTrivia.Split('\n'))
            {
                var trimmedLine = line.Trim('\r').TrimStart();
                if (trimmedLine.StartsWith("///", StringComparison.Ordinal))
                {
                    lines.Add(trimmedLine.Substring(3).TrimEnd());
                }
            }
        }

        return new DocumentationComment { Lines = new EquatableArray<string>(lines) };
    }

    private static DocumentationComment DocumentationFromXml(string xml)
    {
        // <member name="M:…">, the comment's lines indented under it, then </member>.
        var lines = xml.Replace("\r", "").Split('\n').ToList();
        var innerLines = lines.Where(line => !line.TrimStart().StartsWith("<member ", StringComparison.Ordinal) &&
                                             line.Trim() != "</member>" && line.Trim() != "")
            .ToArray();
        if (innerLines.Length == 0)
        {
            return DocumentationComment.Empty;
        }

        var indentation = innerLines.Min(line => line.Length - line.TrimStart().Length);
        return new DocumentationComment
        {
            Lines = new EquatableArray<string>(innerLines.Select(line => " " + line.Substring(indentation).TrimEnd())),
        };
    }

    private static ParameterDeclaration ReadParameter(IParameterSymbol parameter)
    {
        return new ParameterDeclaration
        {
            Name = parameter.Name,
            Type = ReadTypeReference(parameter.Type),
            RefKind = parameter.RefKind,
            IsParams = parameter.IsParams,
            DefaultValueExpression = parameter.HasExplicitDefaultValue ? DefaultValueExpression(parameter.Type, parameter.ExplicitDefaultValue) : null,
            Attributes = ReadAttributes(parameter.GetAttributes()),
        };
    }

    private static string DefaultValueExpression(ITypeSymbol type, object? value)
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
            return EnumValueExpression((INamedTypeSymbol)valueType, value);
        }

        return LiteralExpression(value);
    }

    private static string EnumValueExpression(INamedTypeSymbol enumType, object value)
    {
        var enumMember = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, value));
        var enumTypeName = enumType.ToDisplayString(FullyQualifiedWithoutNullableFormat);
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

    private static AttributeArgument ReadAttributeArgument(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return NewAttributeArgument(AttributeArgumentKind.Null, "null", null, null);
        }

        if (constant.Kind == TypedConstantKind.Array)
        {
            return new AttributeArgument
            {
                Kind = AttributeArgumentKind.Array,
                Expression = constant.ToCSharpString(),
                Value = null,
                Type = constant.Type == null ? null : ReadTypeReference(constant.Type),
                Elements = new EquatableArray<AttributeArgument>(constant.Values.Select(ReadAttributeArgument)),
            };
        }

        if (constant.Kind == TypedConstantKind.Type || constant.Value is ITypeSymbol)
        {
            var namedType = ReadTypeReference((ITypeSymbol)constant.Value!);
            return NewAttributeArgument(AttributeArgumentKind.Type, "typeof(" + namedType.FullyQualifiedName + ")", null, namedType);
        }

        var value = Convert.ToString(constant.Value, CultureInfo.InvariantCulture);
        if (constant.Kind == TypedConstantKind.Enum)
        {
            var enumType = (INamedTypeSymbol)constant.Type!;
            return NewAttributeArgument(AttributeArgumentKind.Enum, EnumValueExpression(enumType, constant.Value!), value, ReadTypeReference(enumType));
        }

        return NewAttributeArgument(AttributeArgumentKind.Primitive, constant.ToCSharpString(), value, null);
    }

    private static AttributeArgument NewAttributeArgument(AttributeArgumentKind kind, string expression, string? value, TypeReference? type)
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

    private static EquatableArray<AttributeDeclaration> ReadAttributes(IEnumerable<AttributeData> attributes)
    {
        return new EquatableArray<AttributeDeclaration>(attributes.Select(ReadAttribute));
    }

    private static TypeReference UnresolvedType(string name)
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

    private static string NamespaceOf(ITypeSymbol type)
    {
        var containingNamespace = type.ContainingNamespace;
        if (containingNamespace == null || containingNamespace.IsGlobalNamespace)
        {
            return "";
        }

        return containingNamespace.ToDisplayString();
    }

    private static TypeReferenceKind KindOf(ITypeSymbol type)
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

    private static bool IsPartial(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var declarations = type.DeclaringSyntaxReferences.Select(syntaxReference => syntaxReference.GetSyntax(cancellationToken))
            .OfType<TypeDeclarationSyntax>()
            .ToArray();
        return declarations.Length > 0 && declarations.All(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static IEnumerable<INamedTypeSymbol> BaseTypesOf(INamedTypeSymbol type)
    {
        var baseType = type.BaseType;
        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }
    }

    private static IEnumerable<IPropertySymbol> PublicInstanceProperties(INamedTypeSymbol type)
    {
        var seenNames = new HashSet<string>();
        for (INamedTypeSymbol? declaringType = type; declaringType != null; declaringType = declaringType.BaseType)
        {
            var properties = declaringType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic && !property.IsIndexer);
            foreach (var property in properties)
            {
                if (seenNames.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    private static IEnumerable<IMethodSymbol> DeclaredMethods(INamedTypeSymbol type)
    {
        return type.GetMembers().OfType<IMethodSymbol>().Where(method => method.MethodKind == MethodKind.Ordinary && !method.IsImplicitlyDeclared);
    }

    private static SetterKind SetterKindOf(IPropertySymbol property)
    {
        if (property.SetMethod == null)
        {
            return SetterKind.None;
        }

        return property.SetMethod.IsInitOnly ? SetterKind.Init : SetterKind.Set;
    }
}
