using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Reads a <c>[LogExecuting]</c> class into plain values: its interfaces as a decorator would implement them, a
///     hand-written <c>Executing:</c> line it still has, and the logged parameters that look like secrets. Which
///     interface is the use case's is decided later, with the compilation's settings and generated interfaces.
/// </summary>
public static class ExecutingLogReader
{
    public const string LogExecutingAttributeMetadataName = "MagicCSharp.UseCases.LogExecutingAttribute";

    private const string ExecuteMethodName = "Execute";

    public static ExecutingLogClassReading Read(INamedTypeSymbol useCaseClass, CancellationToken cancellationToken)
    {
        var resolvedInterfaces = useCaseClass.AllInterfaces.Where(implementedInterface => implementedInterface.TypeKind == TypeKind.Interface).ToArray();
        return new ExecutingLogClassReading
        {
            UseCaseClass = TypeDeclarationReader.Read(useCaseClass, cancellationToken),
            OwnExecutingLineLocation = FindOwnExecutingLine(useCaseClass, cancellationToken),
            ImplementedInterfaces = new EquatableArray<ImplementedInterfaceReading>(resolvedInterfaces.Select(implementedInterface =>
                ReadImplementedInterface(useCaseClass, implementedInterface, cancellationToken))),
            SecretParameters = new EquatableArray<SecretParameterReading>(FindSecretParameters(useCaseClass, resolvedInterfaces, cancellationToken)),
        };
    }

    private static ImplementedInterfaceReading ReadImplementedInterface(INamedTypeSymbol useCaseClass, INamedTypeSymbol implementedInterface,
        CancellationToken cancellationToken)
    {
        return new ImplementedInterfaceReading
        {
            Interface = TypeReferenceReader.Read(implementedInterface),
            DisplayName = implementedInterface.ToDisplayString(),
            ExtendedInterfaceMetadataNames = new EquatableArray<string>(implementedInterface.AllInterfaces.Select(extendedInterface => extendedInterface.ToDisplayString())),
            Methods = new EquatableArray<MethodDeclaration>(InterfaceMethods(implementedInterface)
                .Select(interfaceMethod => ReadAsImplemented(useCaseClass, interfaceMethod, cancellationToken))),
        };
    }

    private static IEnumerable<IMethodSymbol> InterfaceMethods(INamedTypeSymbol implementedInterface)
    {
        return new[] { implementedInterface }.Concat(implementedInterface.AllInterfaces)
            .SelectMany(declaringInterface => declaringInterface.GetMembers().OfType<IMethodSymbol>())
            .Where(method => method.MethodKind == MethodKind.Ordinary && method.IsAbstract && !method.IsStatic);
    }

    // The decorator implements the interface, so it takes the interface's signature; the location that ties a
    // secret to the method is the class's implementation. A parameter carries the attributes of both, so [NotLogged]
    // on either leaves it out of the line.
    private static MethodDeclaration ReadAsImplemented(INamedTypeSymbol useCaseClass, IMethodSymbol interfaceMethod, CancellationToken cancellationToken)
    {
        var interfaceMethodDeclaration = MethodDeclarationReader.Read(interfaceMethod, cancellationToken);
        var implementation = useCaseClass.FindImplementationForInterfaceMember(interfaceMethod) as IMethodSymbol;
        if (implementation == null)
        {
            return interfaceMethodDeclaration;
        }

        var implementationDeclaration = MethodDeclarationReader.Read(implementation, cancellationToken);
        var parameters = interfaceMethodDeclaration.Parameters.Select((parameter, index) => parameter with
        {
            Attributes = new EquatableArray<AttributeDeclaration>(implementationDeclaration.Parameters[index].Attributes.Concat(parameter.Attributes)),
        });
        return interfaceMethodDeclaration with
        {
            Parameters = new EquatableArray<ParameterDeclaration>(parameters),
            Location = implementationDeclaration.Location,
        };
    }

    // The class's public Execute methods (the members of an interface [GenerateInterface] writes) and its
    // implementations of the interfaces the compiler resolves.
    private static IEnumerable<IMethodSymbol> LoggedMethods(INamedTypeSymbol useCaseClass, INamedTypeSymbol[] resolvedInterfaces)
    {
        var publicExecuteMethods = useCaseClass.GetMembers(ExecuteMethodName)
            .OfType<IMethodSymbol>()
            .Where(method => method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public && !method.IsStatic);
        var implementations = resolvedInterfaces.SelectMany(InterfaceMethods)
            .Select(interfaceMethod => useCaseClass.FindImplementationForInterfaceMember(interfaceMethod))
            .OfType<IMethodSymbol>();
        return publicExecuteMethods.Concat(implementations).Distinct<IMethodSymbol>(SymbolEqualityComparer.Default);
    }

    // A decorator logs the names its interface gives the parameters, and the class's own Execute logs its own; a
    // secret under either name is found, once per name.
    private static IEnumerable<SecretParameterReading> FindSecretParameters(INamedTypeSymbol useCaseClass, INamedTypeSymbol[] resolvedInterfaces,
        CancellationToken cancellationToken)
    {
        var classMethodSecrets = LoggedMethods(useCaseClass, resolvedInterfaces)
            .SelectMany(method => FindSecretParameters(method, MethodDeclarationReader.Read(method, cancellationToken)));
        var interfaceSecrets = resolvedInterfaces.SelectMany(InterfaceMethods)
            .Where(interfaceMethod => useCaseClass.FindImplementationForInterfaceMember(interfaceMethod) != null)
            .SelectMany(interfaceMethod => FindSecretParameters(interfaceMethod, ReadAsImplemented(useCaseClass, interfaceMethod, cancellationToken)));
        return classMethodSecrets.Concat(interfaceSecrets).Distinct();
    }

    private static IEnumerable<SecretParameterReading> FindSecretParameters(IMethodSymbol methodWithLoggedNames, MethodDeclaration loggedDeclaration)
    {
        for (var index = 0; index < methodWithLoggedNames.Parameters.Length; index++)
        {
            if (LoggedParameters.IsLogged(loggedDeclaration.Parameters[index]) && SecretParameters.LooksSecret(methodWithLoggedNames.Parameters[index]))
            {
                yield return new SecretParameterReading
                {
                    MethodLocation = loggedDeclaration.Location,
                    ParameterName = methodWithLoggedNames.Parameters[index].Name,
                };
            }
        }
    }

    private static SourceLocation? FindOwnExecutingLine(INamedTypeSymbol useCaseClass, CancellationToken cancellationToken)
    {
        var executingLine = useCaseClass.DeclaringSyntaxReferences.Select(syntaxReference => syntaxReference.GetSyntax(cancellationToken))
            .SelectMany(classSyntax => classSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
            .FirstOrDefault(ExecutingLineSyntax.IsExecutingLine);
        return executingLine == null ? null : SourceLocation.From(executingLine.GetLocation());
    }
}
