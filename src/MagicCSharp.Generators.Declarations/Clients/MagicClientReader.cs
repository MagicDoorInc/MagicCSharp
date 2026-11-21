using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Reads a <c>[MagicClient]</c> interface into its client class and registration, or the reasons it gets none.</summary>
internal static class MagicClientReader
{
    private const string TaskMetadataName = "System.Threading.Tasks.Task";

    private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";

    private const string GuidMetadataName = "System.Guid";

    // Namespace-qualified, without generic arguments: Task and Task<T> both read System.Threading.Tasks.Task.
    private static readonly SymbolDisplayFormat MetadataNameFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None);

    public static ClientGenerationResult Read(string interfaceMetadataName, ClientConvention convention, Compilation compilation, CancellationToken cancellationToken)
    {
        var clientInterface = compilation.Assembly.GetTypeByMetadataName(interfaceMetadataName);
        if (clientInterface == null)
        {
            return ClientGenerationResult.Nothing;
        }

        var interfaceDeclaration = TypeDeclarationReader.Read(clientInterface, cancellationToken);
        var shapeProblems = ShapeProblems(interfaceDeclaration, convention);
        if (shapeProblems.Count > 0)
        {
            return ClientGenerationResult.Refused(shapeProblems);
        }

        var clientName = interfaceDeclaration.Name.Substring(1);
        var handWrittenPart = compilation.Assembly.GetTypeByMetadataName(NamespacePrefix(interfaceDeclaration) + clientName);
        var magicClientAttribute = clientInterface.GetAttributes().First(attribute => AttributeName(attribute) == ClientPipelines.MagicClientAttributeMetadataName);
        var route = (magicClientAttribute.ConstructorArguments[0].Value as string ?? "").Trim('/');

        var methodProblems = new List<DiagnosticInfo>();
        var clientMethods = new List<ClientMethod>();
        foreach (var routeMethod in RouteMethods(clientInterface, route))
        {
            if (handWrittenPart != null && IsHandWritten(handWrittenPart, routeMethod.Method))
            {
                continue;
            }

            var clientMethod = ReadMethod(interfaceDeclaration.Name, routeMethod, methodProblems, cancellationToken);
            if (clientMethod != null)
            {
                clientMethods.Add(clientMethod);
            }
        }

        methodProblems.AddRange(ConventionProblems(interfaceDeclaration, convention, clientMethods));
        if (methodProblems.Count > 0)
        {
            return ClientGenerationResult.Refused(methodProblems);
        }

        var magicClientDeclaration = new MagicClientDeclaration
        {
            Interface = interfaceDeclaration,
            ClientName = clientName,
            UrlSetting = NamedArgument(magicClientAttribute, "UrlSetting"),
            DefaultUrl = NamedArgument(magicClientAttribute, "DefaultUrl"),
            Methods = new EquatableArray<ClientMethod>(clientMethods),
            HasHandWrittenPart = handWrittenPart != null,
            HasHandWrittenConstructor = handWrittenPart != null && handWrittenPart.InstanceConstructors.Any(constructor => !constructor.IsImplicitlyDeclared),
            Convention = convention,
        };
        return ClientGenerationResult.Generated(MagicClientWriter.WriteClientFile(magicClientDeclaration),
            MagicClientWriter.WriteRegistrationFile(magicClientDeclaration));
    }

    private static List<DiagnosticInfo> ShapeProblems(TypeDeclaration interfaceDeclaration, ClientConvention convention)
    {
        var shapeProblems = new List<DiagnosticInfo>();
        if (convention.DeclaringAssemblies.Count > 1)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.MoreThanOneClientConvention, interfaceDeclaration.Location, interfaceDeclaration.Name,
                convention.DeclaringAssemblies[0], convention.DeclaringAssemblies[1]));
        }

        if (interfaceDeclaration.TypeParameters.Count > 0)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.GenericClientInterface, interfaceDeclaration.Location, interfaceDeclaration.Name));
        }

        if (interfaceDeclaration.IsNested)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.NestedClientInterface, interfaceDeclaration.Location, interfaceDeclaration.Name));
        }

        var name = interfaceDeclaration.Name;
        var hasClientName = name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]);
        if (!hasClientName)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.ClientInterfaceNameWithoutI, interfaceDeclaration.Location, name));
        }

        return shapeProblems;
    }

    private static IEnumerable<RouteMethod> RouteMethods(INamedTypeSymbol clientInterface, string route)
    {
        foreach (var method in clientInterface.GetMembers().OfType<IMethodSymbol>().Where(method => method.MethodKind == MethodKind.Ordinary && !method.IsStatic))
        {
            var routeAttribute = method.GetAttributes().FirstOrDefault(attribute => HttpMethodProperty(attribute) != null);
            if (routeAttribute == null)
            {
                continue;
            }

            var template = (routeAttribute.ConstructorArguments.FirstOrDefault().Value as string ?? "").Trim('/');
            yield return new RouteMethod
            {
                Method = method,
                HttpMethodProperty = HttpMethodProperty(routeAttribute)!,
                Path = template.Length == 0 ? route : route + "/" + template,
            };
        }
    }

    // A member the hand-written part already declares, by name and parameter types, is left to it.
    private static bool IsHandWritten(INamedTypeSymbol handWrittenPart, IMethodSymbol method)
    {
        return handWrittenPart.GetMembers(method.Name)
            .OfType<IMethodSymbol>()
            .Any(handWrittenMethod => handWrittenMethod.Parameters.Length == method.Parameters.Length &&
                                      handWrittenMethod.Parameters.Zip(method.Parameters,
                                              (handWrittenParameter, parameter) => SymbolEqualityComparer.Default.Equals(handWrittenParameter.Type, parameter.Type))
                                          .All(isSameType => isSameType));
    }

    private static ClientMethod? ReadMethod(string interfaceName, RouteMethod routeMethod, List<DiagnosticInfo> problems, CancellationToken cancellationToken)
    {
        var method = routeMethod.Method;
        var location = SourceLocation.From(method.Locations.FirstOrDefault());
        var problemCount = problems.Count;
        if (method.IsGenericMethod)
        {
            problems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.GenericClientMethod, location, interfaceName, method.Name));
        }

        var returnType = method.ReturnType as INamedTypeSymbol;
        var isTask = returnType != null && returnType.ToDisplayString(MetadataNameFormat) == TaskMetadataName && returnType.TypeArguments.Length <= 1;
        if (!isTask)
        {
            problems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.ClientMethodNotTask, location, interfaceName, method.Name,
                method.ReturnType.ToDisplayString()));
        }

        var parameters = ReadParameters(interfaceName, routeMethod, problems);
        if (problems.Count > problemCount)
        {
            return null;
        }

        var resultType = returnType!.TypeArguments.FirstOrDefault();
        return new ClientMethod
        {
            Method = MethodDeclarationReader.Read(method, cancellationToken),
            HttpMethodProperty = routeMethod.HttpMethodProperty,
            Path = routeMethod.Path,
            Parameters = new EquatableArray<ClientParameter>(parameters),
            ResultFullyQualifiedName = resultType == null ? null : TypeReferenceReader.Read(resultType).FullyQualifiedName,
            IsNullableResult = resultType != null && TypeReferenceReader.Read(resultType).IsNullable,
        };
    }

    private static List<ClientParameter> ReadParameters(string interfaceName, RouteMethod routeMethod, List<DiagnosticInfo> problems)
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

        var isGet = routeMethod.HttpMethodProperty == "Get";
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

        if (type.ToDisplayString(MetadataNameFormat) == CancellationTokenMetadataName)
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

        if (type.ToDisplayString(MetadataNameFormat) == GuidMetadataName)
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

    private static IEnumerable<DiagnosticInfo> ConventionProblems(TypeDeclaration interfaceDeclaration, ClientConvention convention, List<ClientMethod> clientMethods)
    {
        var neededMembers = new List<string>();
        if (clientMethods.Any(clientMethod => clientMethod.ResultFullyQualifiedName != null))
        {
            neededMembers.Add(ClientBaseMembers.SendRequestWithResponse);
        }

        if (clientMethods.Any(clientMethod => clientMethod.ResultFullyQualifiedName == null))
        {
            neededMembers.Add(ClientBaseMembers.SendRequest);
        }

        if (clientMethods.Any(clientMethod => clientMethod.Parameters.Any(IsQueryParameter)))
        {
            neededMembers.Add(ClientBaseMembers.WithQuery);
        }

        if (clientMethods.Any(clientMethod => clientMethod.IsNullableResult))
        {
            neededMembers.Add(ClientBaseMembers.IsNotFound);
        }

        foreach (var missingMember in neededMembers.Where(neededMember => !convention.BaseMembers.Contains(neededMember)))
        {
            yield return ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.ClientBaseLacksMember, interfaceDeclaration.Location, interfaceDeclaration.Name,
                convention.BaseFullyQualifiedName, missingMember);
        }

        if (!convention.HasRegistrationMethod)
        {
            yield return ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.ClientRegistrationNotFound, interfaceDeclaration.Location, interfaceDeclaration.Name,
                convention.RegistrationTypeFullyQualifiedName, convention.RegistrationMethod);
        }
    }

    private static bool IsQueryParameter(ClientParameter clientParameter)
    {
        return clientParameter.Role == ClientParameterRole.QueryValue || clientParameter.Role == ClientParameterRole.QueryObject;
    }

    private static string? NamedArgument(AttributeData attribute, string name)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument.Key == name)
            {
                return namedArgument.Value.Value as string;
            }
        }

        return null;
    }

    private static string AttributeName(AttributeData attribute)
    {
        return attribute.AttributeClass == null ? "" : attribute.AttributeClass.ToDisplayString(MetadataNameFormat);
    }

    private static string NamespacePrefix(TypeDeclaration type)
    {
        return type.Namespace == "" ? "" : type.Namespace + ".";
    }

    // The HttpMethod property a client route attribute sends with; null for any other attribute.
    private static string? HttpMethodProperty(AttributeData attribute)
    {
        switch (AttributeName(attribute))
        {
            case "MagicCSharp.Clients.ClientGetAttribute":
                return "Get";
            case "MagicCSharp.Clients.ClientPostAttribute":
                return "Post";
            case "MagicCSharp.Clients.ClientPutAttribute":
                return "Put";
            case "MagicCSharp.Clients.ClientPatchAttribute":
                return "Patch";
            case "MagicCSharp.Clients.ClientDeleteAttribute":
                return "Delete";
            default:
                return null;
        }
    }

    // A route-attributed interface method, before it is read: what the attribute says about it.
    private sealed record RouteMethod
    {
        public required IMethodSymbol Method { get; init; }

        public required string HttpMethodProperty { get; init; }

        /// <summary>The client's route and the method's template, trimmed of slashes.</summary>
        public required string Path { get; init; }
    }
}
