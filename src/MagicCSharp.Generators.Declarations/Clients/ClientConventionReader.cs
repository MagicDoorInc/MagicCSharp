using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Finds the client convention of a compilation: a <c>[MagicClientConvention]</c> in it or any assembly it references, or the defaults.</summary>
internal static class ClientConventionReader
{
    private const string ConventionAttributeMetadataName = "MagicCSharp.Clients.MagicClientConventionAttribute";

    private const string DefaultBaseMetadataName = "MagicCSharp.Clients.MagicHttpClientBase";

    private const string DefaultRegistrationMetadataName = "MagicCSharp.Clients.MagicHttpClientRegistration";

    private const string DefaultRegistrationMethod = "AddMagicHttpClient";

    private const string SendRequestMethodName = "SendRequestAsync";

    private const int SendRequestParameterCount = 4;

    private const int RegistrationParameterCount = 3;

    private const int RegistrationTypeParameterCount = 2;

    private static readonly SymbolDisplayFormat FullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    public static ClientConvention Read(Compilation compilation, CancellationToken cancellationToken)
    {
        var conventionAttributeType = compilation.GetTypeByMetadataName(ConventionAttributeMetadataName);
        var conventionDeclarations = conventionAttributeType == null ? [] : ConventionDeclarations(compilation, conventionAttributeType);
        var declaringAssemblies = new EquatableArray<string>(conventionDeclarations.Select(declaration => declaration.Key.Name));
        if (conventionDeclarations.Count == 1)
        {
            var conventionAttribute = conventionDeclarations[0].Value;
            var baseType = conventionAttribute.ConstructorArguments[0].Value as INamedTypeSymbol;
            var registrationType = conventionAttribute.ConstructorArguments[1].Value as INamedTypeSymbol;
            var registrationMethod = conventionAttribute.ConstructorArguments[2].Value as string ?? "";
            return ReadConvention(baseType, registrationType, registrationMethod, cancellationToken) with { DeclaringAssemblies = declaringAssemblies };
        }

        var defaultConvention = ReadConvention(compilation.GetTypeByMetadataName(DefaultBaseMetadataName),
            compilation.GetTypeByMetadataName(DefaultRegistrationMetadataName), DefaultRegistrationMethod, cancellationToken);
        return defaultConvention with { DeclaringAssemblies = declaringAssemblies };
    }

    private static List<KeyValuePair<IAssemblySymbol, AttributeData>> ConventionDeclarations(Compilation compilation, INamedTypeSymbol conventionAttributeType)
    {
        var assemblies = new[] { compilation.Assembly }.Concat(compilation.SourceModule.ReferencedAssemblySymbols);
        return assemblies.SelectMany(assembly => assembly.GetAttributes()
                .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, conventionAttributeType))
                .Select(attribute => new KeyValuePair<IAssemblySymbol, AttributeData>(assembly, attribute)))
            .ToList();
    }

    private static ClientConvention ReadConvention(INamedTypeSymbol? baseType, INamedTypeSymbol? registrationType, string registrationMethod,
        CancellationToken cancellationToken)
    {
        return new ClientConvention
        {
            DeclaringAssemblies = EquatableArray<string>.Empty,
            BaseFullyQualifiedName = baseType == null ? GlobalAlias.Prefix + DefaultBaseMetadataName : baseType.ToDisplayString(FullyQualifiedFormat),
            BaseConstructors = baseType == null
                ? EquatableArray<MethodDeclaration>.Empty
                : new EquatableArray<MethodDeclaration>(CopiedConstructors(baseType).Select(constructor => MethodDeclarationReader.Read(constructor, cancellationToken))),
            BaseMembers = baseType == null ? EquatableArray<string>.Empty : new EquatableArray<string>(OfferedMembers(baseType)),
            RegistrationTypeFullyQualifiedName = registrationType == null
                ? GlobalAlias.Prefix + DefaultRegistrationMetadataName
                : registrationType.ToDisplayString(FullyQualifiedFormat),
            RegistrationMethod = registrationMethod,
            HasRegistrationMethod = registrationType != null && HasRegistrationMethod(registrationType, registrationMethod),
        };
    }

    private static IEnumerable<IMethodSymbol> CopiedConstructors(INamedTypeSymbol baseType)
    {
        return baseType.InstanceConstructors.Where(constructor => constructor.Parameters.Length > 0 && IsVisibleToSubclass(constructor));
    }

    private static List<string> OfferedMembers(INamedTypeSymbol baseType)
    {
        var offeredMembers = new List<string>();
        var methods = TypeAndBases(baseType).SelectMany(type => type.GetMembers()).OfType<IMethodSymbol>().Where(IsVisibleToSubclass).ToList();
        if (methods.Any(method => method.Name == SendRequestMethodName && method.TypeParameters.Length == 1 && method.Parameters.Length == SendRequestParameterCount))
        {
            offeredMembers.Add(ClientBaseMembers.SendRequestWithResponse);
        }

        if (methods.Any(method => method.Name == SendRequestMethodName && method.TypeParameters.Length == 0 && method.Parameters.Length == SendRequestParameterCount))
        {
            offeredMembers.Add(ClientBaseMembers.SendRequest);
        }

        if (methods.Any(method => method.Name == "WithQuery"))
        {
            offeredMembers.Add(ClientBaseMembers.WithQuery);
        }

        if (methods.Any(method => method.Name == "IsNotFound"))
        {
            offeredMembers.Add(ClientBaseMembers.IsNotFound);
        }

        return offeredMembers;
    }

    private static bool HasRegistrationMethod(INamedTypeSymbol registrationType, string registrationMethod)
    {
        return registrationType.GetMembers(registrationMethod)
            .OfType<IMethodSymbol>()
            .Any(method => method.IsStatic && method.TypeParameters.Length == RegistrationTypeParameterCount && method.Parameters.Length == RegistrationParameterCount);
    }

    private static IEnumerable<INamedTypeSymbol> TypeAndBases(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            yield return current;
        }
    }

    private static bool IsVisibleToSubclass(ISymbol member)
    {
        return member.DeclaredAccessibility == Accessibility.Public || member.DeclaredAccessibility == Accessibility.Protected ||
               member.DeclaredAccessibility == Accessibility.ProtectedOrInternal;
    }
}
