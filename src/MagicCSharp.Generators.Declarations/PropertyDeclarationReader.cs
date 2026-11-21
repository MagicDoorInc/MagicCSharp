using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Reads a property symbol into the <see cref="PropertyDeclaration" /> generators write from.</summary>
public static class PropertyDeclarationReader
{
    public static PropertyDeclaration Read(IPropertySymbol property, CancellationToken cancellationToken)
    {
        return new PropertyDeclaration
        {
            Name = property.Name,
            Type = TypeReferenceReader.Read(property.Type),
            Accessibility = property.DeclaredAccessibility,
            IsRequired = property.IsRequired,
            HasGetter = property.GetMethod != null,
            SetterKind = SetterKindOf(property),
            DeclaringTypeFullyQualifiedName = property.ContainingType.ToDisplayString(SymbolDisplayFormats.FullyQualified),
            Documentation = DocumentationCommentReader.Read(property, cancellationToken),
            Attributes = AttributeDeclarationReader.ReadAll(property.GetAttributes()),
            Location = SourceLocation.From(property.Locations.FirstOrDefault()),
        };
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
