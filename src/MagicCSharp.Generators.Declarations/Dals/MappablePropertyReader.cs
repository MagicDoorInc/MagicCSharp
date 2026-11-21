using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>
///     Reads the public instance properties of a type, own and inherited, into <see cref="MappableProperty" />s: what the
///     Dal mapping copies between and what the filter rules match filter fields against.
/// </summary>
public static class MappablePropertyReader
{
    /// <summary>A property redeclared lower in the hierarchy hides the base's, so the nearest declaration of each name wins.</summary>
    public static EquatableArray<MappableProperty> Read(ITypeSymbol type)
    {
        var seenNames = new HashSet<string>();
        var mappableProperties = new List<MappableProperty>();
        for (var declaringType = type; declaringType != null; declaringType = declaringType.BaseType)
        {
            var properties = declaringType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic && !property.IsIndexer);
            foreach (var property in properties)
            {
                if (seenNames.Add(property.Name))
                {
                    mappableProperties.Add(ReadProperty(property));
                }
            }
        }

        return new EquatableArray<MappableProperty>(mappableProperties);
    }

    private static MappableProperty ReadProperty(IPropertySymbol property)
    {
        var setter = property.SetMethod;
        var hasPublicSetter = setter != null && setter.DeclaredAccessibility == Accessibility.Public;
        return new MappableProperty
        {
            Name = property.Name,
            TypeName = TypeReferenceReader.Read(property.Type).FullyQualifiedName,
            IsReadable = property.GetMethod != null && property.GetMethod.DeclaredAccessibility == Accessibility.Public,
            IsSettable = hasPublicSetter && !setter!.IsInitOnly,
            IsInitializable = hasPublicSetter,
            IsRequired = property.IsRequired,
            IsKey = property.GetAttributes().Any(attribute => attribute.AttributeClass != null &&
                                                              DalReader.MetadataNameOf(attribute.AttributeClass) == DataTypeNames.KeyAttribute),
            IsNullableValueType = property.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T,
        };
    }
}
