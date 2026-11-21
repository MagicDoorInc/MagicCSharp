using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Reads one route-attributed method of a client interface into a <see cref="ClientMethod" />, or adds why it cannot be one.</summary>
internal static class ClientMethodReader
{
    public static ClientMethod? Read(string interfaceName, RouteMethod routeMethod, List<DiagnosticInfo> problems, CancellationToken cancellationToken)
    {
        var method = routeMethod.Method;
        var location = SourceLocation.From(method.Locations.FirstOrDefault());
        var problemCount = problems.Count;
        if (method.IsGenericMethod)
        {
            problems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.GenericClientMethod, location, interfaceName, method.Name));
        }

        var returnType = method.ReturnType as INamedTypeSymbol;
        var isTask = returnType != null && ClientSymbolNames.MetadataName(returnType) == ClientSymbolNames.Task && returnType.TypeArguments.Length <= 1;
        if (!isTask)
        {
            problems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.ClientMethodNotTask, location, interfaceName, method.Name,
                method.ReturnType.ToDisplayString()));
        }

        var parameters = ClientParameterReader.Read(interfaceName, routeMethod, problems);
        if (problems.Count > problemCount)
        {
            return null;
        }

        var resultType = returnType!.TypeArguments.FirstOrDefault();
        return new ClientMethod
        {
            Method = MethodDeclarationReader.Read(method, cancellationToken),
            HttpMethod = routeMethod.HttpMethod,
            Path = routeMethod.Path,
            Parameters = new EquatableArray<ClientParameter>(parameters),
            ResultFullyQualifiedName = resultType == null ? null : TypeReferenceReader.Read(resultType).FullyQualifiedName,
            IsNullableResult = resultType != null && TypeReferenceReader.Read(resultType).IsNullable,
        };
    }
}
