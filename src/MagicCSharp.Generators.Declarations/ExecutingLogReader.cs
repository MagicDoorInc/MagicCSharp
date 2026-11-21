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
            SecretParameters = new EquatableArray<SecretParameterReading>(LoggedMethods(useCaseClass, resolvedInterfaces)
                .SelectMany(method => FindSecretParameters(method, cancellationToken))),
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

    // The decorator implements the interface, so it takes the interface's signature; [NotLogged] and the location
    // that ties a secret to the method are on the class's implementation.
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
            Attributes = implementationDeclaration.Parameters[index].Attributes,
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

    private static IEnumerable<SecretParameterReading> FindSecretParameters(IMethodSymbol method, CancellationToken cancellationToken)
    {
        var methodDeclaration = MethodDeclarationReader.Read(method, cancellationToken);
        for (var index = 0; index < method.Parameters.Length; index++)
        {
            if (LoggedParameters.IsLogged(methodDeclaration.Parameters[index]) && SecretParameters.LooksSecret(method.Parameters[index]))
            {
                yield return new SecretParameterReading
                {
                    MethodLocation = methodDeclaration.Location,
                    ParameterName = method.Parameters[index].Name,
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
