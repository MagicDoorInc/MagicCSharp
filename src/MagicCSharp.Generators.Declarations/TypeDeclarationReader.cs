using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Reads a named type into the <see cref="TypeDeclaration" /> generators compare and write from.</summary>
public static class TypeDeclarationReader
{
    public static TypeDeclaration Read(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        return new TypeDeclaration
        {
            Name = type.Name,
            Namespace = TypeReferenceReader.NamespaceOf(type),
            FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormats.FullyQualified),
            Kind = TypeReferenceReader.KindOf(type),
            Accessibility = type.DeclaredAccessibility,
            IsPartial = IsPartial(type, cancellationToken),
            IsAbstract = type.IsAbstract,
            IsStatic = type.IsStatic,
            IsNested = type.ContainingType != null,
            TypeParameters = new EquatableArray<string>(type.TypeParameters.Select(typeParameter => typeParameter.Name)),
            BaseTypes = new EquatableArray<TypeReference>(BaseTypesOf(type).Select(TypeReferenceReader.Read)),
            Interfaces = new EquatableArray<TypeReference>(type.Interfaces.Select(TypeReferenceReader.Read)),
            AllInterfaces = new EquatableArray<TypeReference>(type.AllInterfaces.Select(TypeReferenceReader.Read)),
            Properties = new EquatableArray<PropertyDeclaration>(PublicInstanceProperties(type).Select(property => PropertyDeclarationReader.Read(property, cancellationToken))),
            Methods = new EquatableArray<MethodDeclaration>(DeclaredMethods(type).Select(method => MethodDeclarationReader.Read(method, cancellationToken))),
            Attributes = AttributeDeclarationReader.ReadAll(type.GetAttributes()),
            Documentation = DocumentationCommentReader.Read(type, cancellationToken),
            Location = SourceLocation.From(type.Locations.FirstOrDefault()),
        };
    }

    /// <summary>Whether every declaration of <paramref name="type" /> is <c>partial</c>, so generated code can join it.</summary>
    public static bool IsPartial(INamedTypeSymbol type, CancellationToken cancellationToken)
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
}
