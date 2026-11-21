using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Finds the methods of a <c>[MagicClient]</c> interface that carry a client route attribute, and the route each sends to.</summary>
internal static class ClientRouteMethodReader
{
    public static IEnumerable<RouteMethod> Read(INamedTypeSymbol clientInterface, string route)
    {
        foreach (var method in clientInterface.GetMembers().OfType<IMethodSymbol>().Where(method => method.MethodKind == MethodKind.Ordinary && !method.IsStatic))
        {
            var routeAttribute = method.GetAttributes().FirstOrDefault(attribute => HttpMethodOf(attribute) != null);
            if (routeAttribute == null)
            {
                continue;
            }

            var template = (routeAttribute.ConstructorArguments.FirstOrDefault().Value as string ?? "").Trim('/');
            yield return new RouteMethod
            {
                Method = method,
                HttpMethod = HttpMethodOf(routeAttribute)!.Value,
                Path = template.Length == 0 ? route : route + "/" + template,
            };
        }
    }

    // A member the hand-written part already declares, by name and parameter types, is left to it.
    public static bool IsHandWritten(INamedTypeSymbol handWrittenPart, IMethodSymbol method)
    {
        return handWrittenPart.GetMembers(method.Name)
            .OfType<IMethodSymbol>()
            .Any(handWrittenMethod => handWrittenMethod.Parameters.Length == method.Parameters.Length &&
                                      handWrittenMethod.Parameters.Zip(method.Parameters,
                                              (handWrittenParameter, parameter) => SymbolEqualityComparer.Default.Equals(handWrittenParameter.Type, parameter.Type))
                                          .All(isSameType => isSameType));
    }

    // The HTTP method a client route attribute sends with; null for any other attribute.
    private static ClientHttpMethod? HttpMethodOf(AttributeData attribute)
    {
        switch (ClientSymbolNames.AttributeName(attribute))
        {
            case "MagicCSharp.Clients.ClientGetAttribute":
                return ClientHttpMethod.Get;
            case "MagicCSharp.Clients.ClientPostAttribute":
                return ClientHttpMethod.Post;
            case "MagicCSharp.Clients.ClientPutAttribute":
                return ClientHttpMethod.Put;
            case "MagicCSharp.Clients.ClientPatchAttribute":
                return ClientHttpMethod.Patch;
            case "MagicCSharp.Clients.ClientDeleteAttribute":
                return ClientHttpMethod.Delete;
            default:
                return null;
        }
    }
}
