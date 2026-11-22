using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.ContextFactories;

/// <summary>Reads <c>[GenerateDesignTimeFactory]</c> contexts, hand-written factories and the factory base.</summary>
public static class ContextFactoryReader
{
    public const string GenerateDesignTimeFactoryAttributeMetadataName = "MagicCSharp.Data.EntityFramework.GenerateDesignTimeFactoryAttribute";

    public const string MigrationsDatabaseAttributeMetadataName = "MagicCSharp.Data.EntityFramework.MigrationsDatabaseAttribute";

    public const string DesignTimeFactoryMetadataName = "Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory`1";

    /// <summary>
    ///     Reads a <c>[GenerateDesignTimeFactory]</c> context, with the database name from its <c>[MigrationsDatabase]</c>:
    ///     the one place the name is written, which the migration check reads too.
    /// </summary>
    public static MigrationsDatabaseContext ReadContext(INamedTypeSymbol context)
    {
        var migrationsDatabase = context.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass != null && MetadataNameOf(attribute.AttributeClass) == MigrationsDatabaseAttributeMetadataName);
        var databaseName = migrationsDatabase != null && migrationsDatabase.ConstructorArguments.Length == 1
            ? migrationsDatabase.ConstructorArguments[0].Value as string
            : null;
        return new MigrationsDatabaseContext
        {
            Name = context.Name,
            Namespace = context.ContainingNamespace.IsGlobalNamespace ? "" : context.ContainingNamespace.ToDisplayString(),
            FullyQualifiedName = context.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Accessibility = context.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
            HasMigrationsDatabase = migrationsDatabase != null,
            DatabaseName = databaseName,
            BaseTypesAndInterfaces = new EquatableArray<string>(BaseTypesAndInterfaces(context)),
            IsNested = context.ContainingType != null,
            IsGeneric = context.IsGenericType,
            IsAbstract = context.IsAbstract,
            Location = SourceLocation.From(context.Locations.FirstOrDefault()),
        };
    }

    /// <summary>
    ///     The <c>global::</c>-qualified context <paramref name="type" /> is a design-time factory for, or null when it is
    ///     not one.
    /// </summary>
    public static string? ReadFactoryContext(INamedTypeSymbol type)
    {
        var designTimeFactory = type.AllInterfaces.FirstOrDefault(implemented =>
            implemented.TypeArguments.Length == 1 && MetadataNameOf(implemented.OriginalDefinition) == DesignTimeFactoryMetadataName);
        return designTimeFactory?.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    public static ContextFactoryBase ReadBase(Compilation compilation, string metadataName)
    {
        var baseType = compilation.GetTypeByMetadataName(metadataName);
        if (baseType == null)
        {
            return new ContextFactoryBase
            {
                MetadataName = metadataName,
                IsFound = false,
                UnusableReason = null,
                FullyQualifiedNameWithoutTypeArguments = "",
                ContextConstraints = EquatableArray<string>.Empty,
            };
        }

        return new ContextFactoryBase
        {
            MetadataName = metadataName,
            IsFound = true,
            UnusableReason = UnusableReason(baseType),
            FullyQualifiedNameWithoutTypeArguments = baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGenericsOptions(SymbolDisplayGenericsOptions.None)),
            ContextConstraints = new EquatableArray<string>(baseType.TypeParameters.SelectMany(typeParameter => typeParameter.ConstraintTypes)
                .Select(constraint => constraint.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))),
        };
    }

    private static string? UnusableReason(INamedTypeSymbol baseType)
    {
        if (baseType.TypeKind != TypeKind.Class || baseType.IsStatic || baseType.IsSealed)
        {
            return "is not a class that can be derived from";
        }

        if (baseType.Arity != 1)
        {
            return "does not have exactly one type parameter";
        }

        var hasNameConstructor = baseType.InstanceConstructors.Any(constructor =>
            constructor.DeclaredAccessibility != Accessibility.Private && constructor.DeclaredAccessibility != Accessibility.Internal &&
            constructor.Parameters.Length >= 1 && constructor.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            constructor.Parameters.Skip(1).All(parameter => parameter.HasExplicitDefaultValue));
        return hasNameConstructor ? null : "has no constructor taking the database name first, with every other parameter optional";
    }

    private static IEnumerable<string> BaseTypesAndInterfaces(INamedTypeSymbol context)
    {
        for (var baseType = context.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            yield return baseType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        foreach (var implemented in context.AllInterfaces)
        {
            yield return implemented.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }

    private static string MetadataNameOf(INamedTypeSymbol type)
    {
        var namespacePrefix = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".";
        return namespacePrefix + type.MetadataName;
    }
}
