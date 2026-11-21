using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Reads the parameters of a client method and decides where each goes in the request: route, query, body or the cancellation token.</summary>
internal static class ClientParameterReader
{
    public static List<ClientParameter> Read(string interfaceName, RouteMethod routeMethod, List<DiagnosticInfo> problems)
    {
        var method = routeMethod.Method;
        var location = SourceLocation.From(method.Locations.FirstOrDefault());
        var routeValueNames = RoutePath.PlaceholderNames(routeMethod.Path);
        foreach (var routeValueName in routeValueNames)
        {
            if (!method.Parameters.Any(parameter => string.Equals(parameter.Name, routeValueName, StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.RouteValueWithoutParameter, location, interfaceName, method.Name,
                    routeMethod.Path, routeValueName));
            }
        }

        var isGet = routeMethod.HttpMethod == ClientHttpMethod.Get;
        var clientParameters = new List<ClientParameter>();
        foreach (var parameter in method.Parameters)
        {
            var isRouteValue = routeValueNames.Any(routeValueName => string.Equals(parameter.Name, routeValueName, StringComparison.OrdinalIgnoreCase));
            var routeExpression = isRouteValue ? RouteExpression(parameter) : null;
            if (isRouteValue && routeExpression == null)
            {
                problems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.UnsupportedRouteValueType, location, interfaceName, method.Name,
                    parameter.Name, parameter.Type.ToDisplayString()));
            }

            clientParameters.Add(new ClientParameter
            {
                Name = UseCaseInterfaceWriter.EscapedName(parameter.Name),
                Role = ParameterRole(parameter.Type, isRouteValue, isGet),
                RouteExpression = routeExpression,
            });
        }

        var bodyParameters = clientParameters.Where(clientParameter => clientParameter.Role == ClientParameterRole.Body).ToList();
        if (bodyParameters.Count > 1)
        {
            problems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.MoreThanOneBodyParameter, location, interfaceName, method.Name,
                string.Join(", ", bodyParameters.Select(bodyParameter => bodyParameter.Name))));
        }

        return clientParameters;
    }

    private static ClientParameterRole ParameterRole(ITypeSymbol type, bool isRouteValue, bool isGet)
    {
        if (isRouteValue)
        {
            return ClientParameterRole.RouteValue;
        }

        if (ClientSymbolNames.MetadataName(type) == ClientSymbolNames.CancellationToken)
        {
            return ClientParameterRole.CancellationToken;
        }

        if (!IsObject(type))
        {
            return ClientParameterRole.QueryValue;
        }

        return isGet ? ClientParameterRole.QueryObject : ClientParameterRole.Body;
    }

    // A class or interface that is not a string or a collection: its properties are the values, not itself.
    private static bool IsObject(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Interface)
        {
            return false;
        }

        var isCollection = type.SpecialType == SpecialType.System_Collections_IEnumerable ||
                           type.AllInterfaces.Any(implemented => implemented.SpecialType == SpecialType.System_Collections_IEnumerable);
        return !isCollection;
    }

    // The C# that writes a route value into the path; null for a type a route value cannot be.
    private static string? RouteExpression(IParameterSymbol parameter)
    {
        var name = UseCaseInterfaceWriter.EscapedName(parameter.Name);
        var type = parameter.Type;
        if (type.SpecialType == SpecialType.System_String)
        {
            return "global::System.Uri.EscapeDataString(" + name + ")";
        }

        if (IsInteger(type))
        {
            return name + ".ToString(global::System.Globalization.CultureInfo.InvariantCulture)";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return "global::System.Uri.EscapeDataString(" + name + ".ToString())";
        }

        if (ClientSymbolNames.MetadataName(type) == ClientSymbolNames.Guid)
        {
            return name + ".ToString()";
        }

        return null;
    }

    private static bool IsInteger(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return true;
            default:
                return false;
        }
    }
}
