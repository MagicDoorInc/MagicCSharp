using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>
///     Reads a class implementing <c>IDalTransform&lt;TEntity, TEdit&gt;</c> into a <see cref="DalDeclaration" />. Any
///     other class, and an abstract Dal base, reads as no Dal.
/// </summary>
public static class DalReader
{
    private const string ToEntityMethodName = "ToEntity";
    private const string ApplyMethodName = "Apply";
    private const string FromMethodName = "From";

    internal static DalReadResult NotADal { get; } = new DalReadResult
    {
        Declaration = null,
        Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
    };

    public static DalReadResult Read(INamedTypeSymbol dalClass, CancellationToken cancellationToken)
    {
        var dalTransform = FindInterface(dalClass, DataTypeNames.DalTransform);
        if (dalTransform == null || dalClass.TypeKind != TypeKind.Class || dalClass.IsAbstract || dalClass.IsStatic)
        {
            return NotADal;
        }

        var entityType = dalTransform.TypeArguments[0];
        var editType = dalTransform.TypeArguments[1];
        if (entityType.TypeKind == TypeKind.Error || editType.TypeKind == TypeKind.Error)
        {
            return NotADal;
        }

        var dal = DeclarationReader.ReadType(dalClass, cancellationToken);
        var primaryKind = PrimaryKindOf(dalClass);
        var ownToEntity = FindMappingMethod(dalClass, ToEntityMethodName, method => method.Parameters.Length == 0);
        var ownApply = FindMappingMethod(dalClass, ApplyMethodName,
            method => method.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, editType));
        var ownFrom = FindOwnFrom(dalClass, editType, primaryKind);

        var isMissingAMethod = !ownToEntity.IsImplemented || !ownApply.IsImplemented || (primaryKind != DalPrimaryKind.None && ownFrom == null);
        if (dal.IsNested || dal.TypeParameters.Count > 0)
        {
            if (!dal.IsPartial || !isMissingAMethod)
            {
                return NotADal;
            }

            var shapeDescriptor = dal.IsNested ? DalMappingDiagnostics.NestedDal : DalMappingDiagnostics.GenericDal;
            return Refused(shapeDescriptor, dal);
        }

        var dalProperties = ReadMappableProperties(dalClass);
        return new DalReadResult
        {
            Declaration = new DalDeclaration
            {
                Dal = dal,
                Entity = DeclarationReader.ReadTypeReference(entityType),
                Edit = DeclarationReader.ReadTypeReference(editType),
                DalProperties = dalProperties,
                EntityProperties = ReadMappableProperties(entityType),
                EditProperties = ReadMappableProperties(editType),
                PrimaryKind = primaryKind,
                TenantKeyPropertyName = FindTenantKeyPropertyName(dalClass),
                HasOwnToEntity = ownToEntity.IsImplemented,
                HasOwnApply = ownApply.IsImplemented,
                HasOwnFrom = ownFrom != null,
                IsOwnFromPublic = ownFrom != null && ownFrom.DeclaredAccessibility == Accessibility.Public,
                ShouldOverrideToEntity = ownToEntity.IsAbstractInBase,
                ShouldOverrideApply = ownApply.IsAbstractInBase,
                HasEntityParameterlessConstructor = HasAccessibleParameterlessConstructor(entityType, dalClass.ContainingAssembly),
                CanAddDalFromEdit = primaryKind == DalPrimaryKind.Id && FindInterface(dalClass, DataTypeNames.DalFromEdit) == null &&
                                    dalTransform.OriginalDefinition.ContainingAssembly.GetTypeByMetadataName(DataTypeNames.DalFromEdit) != null,
            },
            Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
        };
    }

    internal static string MetadataNameOf(INamedTypeSymbol type)
    {
        var containingNamespace = type.ContainingNamespace;
        if (containingNamespace == null || containingNamespace.IsGlobalNamespace)
        {
            return type.MetadataName;
        }

        return containingNamespace.ToDisplayString() + "." + type.MetadataName;
    }

    private static INamedTypeSymbol? FindInterface(INamedTypeSymbol type, string metadataName)
    {
        return type.AllInterfaces.FirstOrDefault(implemented => MetadataNameOf(implemented.OriginalDefinition) == metadataName);
    }

    private static DalPrimaryKind PrimaryKindOf(INamedTypeSymbol dalClass)
    {
        if (FindInterface(dalClass, DataTypeNames.DalId) != null)
        {
            return DalPrimaryKind.Id;
        }

        if (FindInterface(dalClass, DataTypeNames.DalKey) != null)
        {
            return DalPrimaryKind.Key;
        }

        return DalPrimaryKind.None;
    }

    // A method the Dal declares counts as its own, and so does a concrete one it inherits: generating another would
    // replace or hide it. Only an abstract one in a base class is left for the generator, which then overrides it.
    private static MappingMethodState FindMappingMethod(INamedTypeSymbol dalClass, string methodName, Func<IMethodSymbol, bool> hasMappingSignature)
    {
        for (INamedTypeSymbol? declaringType = dalClass; declaringType != null; declaringType = declaringType.BaseType)
        {
            var method = declaringType.GetMembers(methodName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(candidate => !candidate.IsStatic && candidate.MethodKind == MethodKind.Ordinary && hasMappingSignature(candidate));
            if (method == null)
            {
                continue;
            }

            var isAbstractInBase = method.IsAbstract && !SymbolEqualityComparer.Default.Equals(declaringType, dalClass);
            return new MappingMethodState { IsImplemented = !isAbstractInBase, IsAbstractInBase = isAbstractInBase };
        }

        return new MappingMethodState { IsImplemented = false, IsAbstractInBase = false };
    }

    private static IMethodSymbol? FindOwnFrom(INamedTypeSymbol dalClass, ITypeSymbol editType, DalPrimaryKind primaryKind)
    {
        if (primaryKind == DalPrimaryKind.None)
        {
            return null;
        }

        var primarySpecialType = primaryKind == DalPrimaryKind.Id ? SpecialType.System_Int64 : SpecialType.System_String;
        return dalClass.GetMembers(FromMethodName)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => method.IsStatic && method.Parameters.Length == 2 &&
                                      SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, editType) &&
                                      method.Parameters[1].Type.SpecialType == primarySpecialType);
    }

    private static EquatableArray<MappableProperty> ReadMappableProperties(ITypeSymbol type)
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
                    mappableProperties.Add(ReadMappableProperty(property));
                }
            }
        }

        return new EquatableArray<MappableProperty>(mappableProperties);
    }

    private static MappableProperty ReadMappableProperty(IPropertySymbol property)
    {
        var setter = property.SetMethod;
        var hasPublicSetter = setter != null && setter.DeclaredAccessibility == Accessibility.Public;
        return new MappableProperty
        {
            Name = property.Name,
            TypeName = DeclarationReader.ReadTypeReference(property.Type).FullyQualifiedName,
            IsReadable = property.GetMethod != null && property.GetMethod.DeclaredAccessibility == Accessibility.Public,
            IsSettable = hasPublicSetter && !setter!.IsInitOnly,
            IsInitializable = hasPublicSetter,
            IsRequired = property.IsRequired,
            IsKey = property.GetAttributes().Any(attribute => attribute.AttributeClass != null &&
                                                              MetadataNameOf(attribute.AttributeClass) == DataTypeNames.KeyAttribute),
        };
    }

    private static string? FindTenantKeyPropertyName(INamedTypeSymbol dalClass)
    {
        var tenantKey = dalClass.ContainingAssembly.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass != null && MetadataNameOf(attribute.AttributeClass) == DataTypeNames.TenantKeyAttribute);
        if (tenantKey == null || tenantKey.ConstructorArguments.Length != 1)
        {
            return null;
        }

        var propertyName = tenantKey.ConstructorArguments[0].Value as string;
        for (INamedTypeSymbol? declaringType = dalClass; declaringType != null; declaringType = declaringType.BaseType)
        {
            var tenantKeyProperty = declaringType.GetMembers(propertyName ?? "")
                .OfType<IPropertySymbol>()
                .FirstOrDefault(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic);
            if (tenantKeyProperty != null)
            {
                return IsLongOrNullableLong(tenantKeyProperty.Type) ? tenantKeyProperty.Name : null;
            }
        }

        return null;
    }

    private static bool IsLongOrNullableLong(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Int64)
        {
            return true;
        }

        var namedType = type as INamedTypeSymbol;
        return namedType != null && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
               namedType.TypeArguments[0].SpecialType == SpecialType.System_Int64;
    }

    private static bool HasAccessibleParameterlessConstructor(ITypeSymbol entityType, IAssemblySymbol dalAssembly)
    {
        var entityClass = entityType as INamedTypeSymbol;
        if (entityClass == null || entityClass.IsAbstract || entityClass.IsStatic)
        {
            return false;
        }

        if (entityClass.TypeKind == TypeKind.Struct)
        {
            return true;
        }

        if (entityClass.TypeKind != TypeKind.Class)
        {
            return false;
        }

        var isSameAssembly = SymbolEqualityComparer.Default.Equals(entityClass.ContainingAssembly, dalAssembly);
        return entityClass.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0 &&
                                                                   (constructor.DeclaredAccessibility == Accessibility.Public ||
                                                                    (isSameAssembly && (constructor.DeclaredAccessibility == Accessibility.Internal ||
                                                                                        constructor.DeclaredAccessibility ==
                                                                                        Accessibility.ProtectedOrInternal))));
    }

    private static DalReadResult Refused(DiagnosticDescriptor descriptor, TypeDeclaration dal)
    {
        return new DalReadResult
        {
            Declaration = null,
            Diagnostics = new EquatableArray<DiagnosticInfo>(
            [
                new DiagnosticInfo
                {
                    Descriptor = descriptor,
                    Location = dal.Location,
                    MessageArguments = new EquatableArray<string>([dal.Name]),
                },
            ]),
        };
    }

    private struct MappingMethodState
    {
        public bool IsImplemented { get; set; }

        public bool IsAbstractInBase { get; set; }
    }
}
