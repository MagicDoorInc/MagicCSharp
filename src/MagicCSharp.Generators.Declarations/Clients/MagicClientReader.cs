using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Reads a <c>[MagicClient]</c> interface into its client class and registration, or the reasons it gets none.</summary>
internal static class MagicClientReader
{
    public static ClientGenerationResult Read(string interfaceMetadataName, ClientConvention convention, Compilation compilation, CancellationToken cancellationToken)
    {
        var clientInterface = compilation.Assembly.GetTypeByMetadataName(interfaceMetadataName);
        if (clientInterface == null)
        {
            return ClientGenerationResult.Nothing;
        }

        var interfaceDeclaration = TypeDeclarationReader.Read(clientInterface, cancellationToken);
        var shapeProblems = ClientShapeProblems.Find(interfaceDeclaration, convention);
        if (shapeProblems.Count > 0)
        {
            return ClientGenerationResult.Refused(shapeProblems);
        }

        var clientName = interfaceDeclaration.Name.Substring(1);
        var handWrittenPart = compilation.Assembly.GetTypeByMetadataName(NamespacePrefix(interfaceDeclaration) + clientName);
        var magicClientAttribute = clientInterface.GetAttributes().First(attribute => ClientSymbolNames.AttributeName(attribute) == ClientPipelines.MagicClientAttributeMetadataName);
        var route = (magicClientAttribute.ConstructorArguments[0].Value as string ?? "").Trim('/');

        var methodProblems = new List<DiagnosticInfo>();
        var clientMethods = new List<ClientMethod>();
        foreach (var routeMethod in ClientRouteMethodReader.Read(clientInterface, route))
        {
            if (handWrittenPart != null && ClientRouteMethodReader.IsHandWritten(handWrittenPart, routeMethod.Method))
            {
                continue;
            }

            var clientMethod = ClientMethodReader.Read(interfaceDeclaration.Name, routeMethod, methodProblems, cancellationToken);
            if (clientMethod != null)
            {
                clientMethods.Add(clientMethod);
            }
        }

        methodProblems.AddRange(ClientConventionProblems.Find(interfaceDeclaration, convention, clientMethods));
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

    private static string NamespacePrefix(TypeDeclaration type)
    {
        return type.Namespace == "" ? "" : type.Namespace + ".";
    }
}
