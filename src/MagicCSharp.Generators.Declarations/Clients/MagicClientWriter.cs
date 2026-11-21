using Microsoft.CodeAnalysis.CSharp;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Writes a <c>[MagicClient]</c> interface's client class and its registration.</summary>
internal static class MagicClientWriter
{
    private const string CancellationTokenNone = "global::System.Threading.CancellationToken.None";

    public static GeneratedFile WriteClientFile(MagicClientDeclaration declaration)
    {
        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();
        WriteNamespace(sourceWriter, declaration.Interface);

        // The hand-written part, when there is one, carries the class's accessibility; two parts saying it
        // differently do not compile.
        var accessibilityPrefix = declaration.HasHandWrittenPart ? "" : UseCaseInterfaceWriter.AccessibilityKeyword(declaration.Interface.Accessibility) + " ";
        sourceWriter.WriteLine(accessibilityPrefix + "partial class " + declaration.ClientName + " : " + declaration.Convention.BaseFullyQualifiedName + ", " +
                               declaration.Interface.FullyQualifiedName);
        sourceWriter.OpenBlock();
        var isFirstMember = true;
        if (!declaration.HasHandWrittenConstructor)
        {
            foreach (var baseConstructorMethodDeclaration in declaration.Convention.BaseConstructors)
            {
                WriteMemberSeparator(sourceWriter, ref isFirstMember);
                WriteConstructor(sourceWriter, declaration.ClientName, baseConstructorMethodDeclaration);
            }
        }

        foreach (var clientMethod in declaration.Methods)
        {
            WriteMemberSeparator(sourceWriter, ref isFirstMember);
            WriteMethod(sourceWriter, clientMethod);
        }

        sourceWriter.CloseBlock();

        return new GeneratedFile
        {
            HintName = HintNames.For(declaration.Interface, "Client"),
            Text = sourceWriter.ToString(),
        };
    }

    public static GeneratedFile WriteRegistrationFile(MagicClientDeclaration declaration)
    {
        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();
        WriteNamespace(sourceWriter, declaration.Interface);

        var accessibility = UseCaseInterfaceWriter.AccessibilityKeyword(declaration.Interface.Accessibility);
        sourceWriter.WriteLine(accessibility + " static class " + declaration.ClientName + "Registration");
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine("/// <summary>Registers " + declaration.ClientName + " as " + declaration.Interface.Name + ".</summary>");
        sourceWriter.WriteLine("public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection Add" + declaration.ClientName +
                               "(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, global::Microsoft.Extensions.Configuration.IConfiguration configuration)");
        sourceWriter.OpenBlock();
        var clientFullyQualifiedName = GlobalAlias.Prefix + NamespacePrefix(declaration.Interface) + declaration.ClientName;
        sourceWriter.WriteLine("return " + declaration.Convention.RegistrationTypeFullyQualifiedName + "." + declaration.Convention.RegistrationMethod + "<" +
                               declaration.Interface.FullyQualifiedName + ", " + clientFullyQualifiedName + ">(services, configuration, " +
                               BaseUrlExpression(declaration) + ");");
        sourceWriter.CloseBlock();
        sourceWriter.CloseBlock();

        return new GeneratedFile
        {
            HintName = HintNames.For(declaration.Interface, "Registration"),
            Text = sourceWriter.ToString(),
        };
    }

    private static void WriteConstructor(SourceWriter sourceWriter, string clientName, MethodDeclaration baseConstructor)
    {
        var arguments = string.Join(", ", baseConstructor.Parameters.Select(parameter => UseCaseInterfaceWriter.EscapedName(parameter.Name)));
        sourceWriter.WriteLine("public " + clientName + "(" + UseCaseInterfaceWriter.ParameterList(baseConstructor) + ") : base(" + arguments + ")");
        sourceWriter.OpenBlock();
        sourceWriter.CloseBlock();
    }

    private static void WriteMethod(SourceWriter sourceWriter, ClientMethod clientMethod)
    {
        var method = clientMethod.Method;
        sourceWriter.WriteLine("public async " + method.ReturnType.FullyQualifiedName + " " + method.Name + "(" + UseCaseInterfaceWriter.ParameterList(method) + ")");
        sourceWriter.OpenBlock();
        var sendArguments = "global::System.Net.Http.HttpMethod." + clientMethod.HttpMethod + ", " + PathExpression(clientMethod) + ", " +
                            BodyExpression(clientMethod) + ", " + CancellationTokenExpression(clientMethod);
        if (clientMethod.ResultFullyQualifiedName == null)
        {
            sourceWriter.WriteLine("await SendRequestAsync(" + sendArguments + ");");
        }
        else if (!clientMethod.IsNullableResult)
        {
            sourceWriter.WriteLine("return await SendRequestAsync<" + clientMethod.ResultFullyQualifiedName + ">(" + sendArguments + ");");
        }
        else
        {
            WriteNullWhenNotFound(sourceWriter, "return await SendRequestAsync<" + clientMethod.ResultFullyQualifiedName + ">(" + sendArguments + ");");
        }

        sourceWriter.CloseBlock();
    }

    private static void WriteNullWhenNotFound(SourceWriter sourceWriter, string sendStatement)
    {
        sourceWriter.WriteLine("try");
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine(sendStatement);
        sourceWriter.CloseBlock();
        sourceWriter.WriteLine("catch (global::System.Exception exception) when (IsNotFound(exception))");
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine("return null;");
        sourceWriter.CloseBlock();
    }

    private static string PathExpression(ClientMethod clientMethod)
    {
        var routeExpressions = clientMethod.Parameters.Where(parameter => parameter.Role == ClientParameterRole.RouteValue)
            .Select(parameter => new KeyValuePair<string, string>(parameter.Name.TrimStart('@'), parameter.RouteExpression!))
            .ToList();
        var pathExpression = RoutePath.Expression(clientMethod.Path, routeExpressions);

        var queryArguments = clientMethod.Parameters.Where(parameter => parameter.Role == ClientParameterRole.QueryObject)
            .Select(parameter => parameter.Name)
            .ToList();
        var queryValueNames = clientMethod.Parameters.Where(parameter => parameter.Role == ClientParameterRole.QueryValue)
            .Select(parameter => parameter.Name)
            .ToList();
        if (queryValueNames.Count > 0)
        {
            queryArguments.Add("new { " + string.Join(", ", queryValueNames) + " }");
        }

        if (queryArguments.Count == 0)
        {
            return pathExpression;
        }

        return "WithQuery(" + pathExpression + ", " + string.Join(", ", queryArguments) + ")";
    }

    // A POST, PUT or PATCH without a body object still sends {}, which a server reading a body accepts; a GET or
    // DELETE without one sends no body.
    private static string BodyExpression(ClientMethod clientMethod)
    {
        var bodyParameter = clientMethod.Parameters.FirstOrDefault(parameter => parameter.Role == ClientParameterRole.Body);
        if (bodyParameter != null)
        {
            return bodyParameter.Name;
        }

        var isWriteWithBody = clientMethod.HttpMethod == ClientHttpMethod.Post || clientMethod.HttpMethod == ClientHttpMethod.Put || clientMethod.HttpMethod == ClientHttpMethod.Patch;
        return isWriteWithBody ? "new object()" : "null";
    }

    private static string CancellationTokenExpression(ClientMethod clientMethod)
    {
        var cancellationTokenParameter = clientMethod.Parameters.FirstOrDefault(parameter => parameter.Role == ClientParameterRole.CancellationToken);
        return cancellationTokenParameter == null ? CancellationTokenNone : cancellationTokenParameter.Name;
    }

    private static string BaseUrlExpression(MagicClientDeclaration declaration)
    {
        var urlSettingExpression = declaration.UrlSetting == null ? null : "configuration[" + SymbolDisplay.FormatLiteral(declaration.UrlSetting, true) + "]";
        var defaultUrlExpression = declaration.DefaultUrl == null ? null : SymbolDisplay.FormatLiteral(declaration.DefaultUrl, true);
        if (urlSettingExpression != null && defaultUrlExpression != null)
        {
            return urlSettingExpression + " ?? " + defaultUrlExpression;
        }

        return urlSettingExpression ?? defaultUrlExpression ?? "null";
    }

    private static void WriteNamespace(SourceWriter sourceWriter, TypeDeclaration clientInterface)
    {
        if (clientInterface.Namespace != "")
        {
            sourceWriter.WriteLine("namespace " + clientInterface.Namespace + ";");
            sourceWriter.WriteLine();
        }
    }

    private static void WriteMemberSeparator(SourceWriter sourceWriter, ref bool isFirstMember)
    {
        if (!isFirstMember)
        {
            sourceWriter.WriteLine();
        }

        isFirstMember = false;
    }

    private static string NamespacePrefix(TypeDeclaration type)
    {
        return type.Namespace == "" ? "" : type.Namespace + ".";
    }
}
