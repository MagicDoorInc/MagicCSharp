using System.Reflection;
using System.Text.RegularExpressions;
using MagicCSharp.Clients;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Testing.Clients;

/// <summary>
///     Compares the routes of the <c>[MagicClient]</c> interfaces in a client library with the controller routes of
///     the service they call. Called from the server's test project, which references both: the server itself never
///     references its own client.
/// </summary>
public static class ClientRouteCheck
{
    private static readonly Regex Placeholder = new Regex(@"\{[^}]*\}", RegexOptions.Compiled);

    /// <summary>
    ///     Reads every route-attributed method of every <c>[MagicClient]</c> interface in <paramref name="clientAssembly" />,
    ///     and every attribute-routed controller action in <paramref name="serverAssemblies" /> through MVC's own action
    ///     model, without starting a host or constructing a controller. A client route is served when an action accepts
    ///     its verb on the same path: compared case-insensitively, any <c>{…}</c> equal to any other whatever its name or
    ///     constraint, and an optional trailing <c>{…?}</c> matching with or without it.
    /// </summary>
    public static ClientRouteCheckResult CheckClientRoutes(Assembly clientAssembly, params Assembly[] serverAssemblies)
    {
        var clientRoutes = ReadClientRoutes(clientAssembly);
        var serverRoutes = ReadServerRoutes(serverAssemblies);
        var unservedClientRoutes = clientRoutes.Where(clientRoute => !serverRoutes.Any(serverRoute => Serves(serverRoute, clientRoute))).ToList();
        return new ClientRouteCheckResult
        {
            ClientRoutes = clientRoutes,
            ServerRoutes = serverRoutes,
            UnservedClientRoutes = unservedClientRoutes,
        };
    }

    /// <summary>
    ///     Throws when a client route is served by no server route, and when either side has no routes at all, which
    ///     means the test passed the wrong assembly.
    /// </summary>
    public static void AssertEveryClientRouteIsServed(Assembly clientAssembly, params Assembly[] serverAssemblies)
    {
        var clientRouteCheckResult = CheckClientRoutes(clientAssembly, serverAssemblies);
        if (clientRouteCheckResult.ClientRoutes.Count == 0)
        {
            throw new InvalidOperationException($"{clientAssembly.GetName().Name} has no [MagicClient] interface with a client route attribute.");
        }

        if (clientRouteCheckResult.ServerRoutes.Count == 0)
        {
            var serverAssemblyNames = string.Join(", ", serverAssemblies.Select(serverAssembly => serverAssembly.GetName().Name));
            throw new InvalidOperationException($"No attribute-routed controller action found in {serverAssemblyNames}.");
        }

        if (clientRouteCheckResult.UnservedClientRoutes.Count > 0)
        {
            var unservedLines = clientRouteCheckResult.UnservedClientRoutes.Select(clientRoute =>
                $"{clientRoute.Client}.{clientRoute.Method}: {clientRoute.HttpMethod} {clientRoute.Path}");
            throw new InvalidOperationException("No controller action serves these client routes:\n" + string.Join("\n", unservedLines));
        }
    }

    private static List<ClientRoute> ReadClientRoutes(Assembly clientAssembly)
    {
        var clientRoutes = new List<ClientRoute>();
        var clientInterfaces = clientAssembly.GetTypes().Where(type => type.IsInterface && type.GetCustomAttribute<MagicClientAttribute>() != null);
        foreach (var clientInterface in clientInterfaces)
        {
            var magicClientAttribute = clientInterface.GetCustomAttribute<MagicClientAttribute>()!;
            foreach (var method in clientInterface.GetMethods())
            {
                var clientRouteAttribute = method.GetCustomAttribute<ClientRouteAttribute>();
                if (clientRouteAttribute == null)
                {
                    continue;
                }

                clientRoutes.Add(new ClientRoute
                {
                    Client = clientInterface.Name,
                    Method = method.Name,
                    HttpMethod = clientRouteAttribute.HttpMethod,
                    Path = CombinePath(magicClientAttribute.Route, clientRouteAttribute.Template),
                });
            }
        }

        return clientRoutes;
    }

    private static List<ServerRoute> ReadServerRoutes(Assembly[] serverAssemblies)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var mvcBuilder = services.AddControllers();
        mvcBuilder.PartManager.ApplicationParts.Clear();
        foreach (var serverAssembly in serverAssemblies)
        {
            mvcBuilder.AddApplicationPart(serverAssembly);
        }

        using var serviceProvider = services.BuildServiceProvider();
        var actionDescriptorCollectionProvider = serviceProvider.GetRequiredService<IActionDescriptorCollectionProvider>();
        return actionDescriptorCollectionProvider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(controllerActionDescriptor => controllerActionDescriptor.AttributeRouteInfo != null && controllerActionDescriptor.AttributeRouteInfo.Template != null)
            .Select(controllerActionDescriptor => new ServerRoute
            {
                Controller = controllerActionDescriptor.ControllerName,
                Action = controllerActionDescriptor.ActionName,
                HttpMethods = AcceptedHttpMethods(controllerActionDescriptor),
                Path = controllerActionDescriptor.AttributeRouteInfo!.Template!,
            })
            .ToList();
    }

    private static List<string> AcceptedHttpMethods(ControllerActionDescriptor controllerActionDescriptor)
    {
        if (controllerActionDescriptor.ActionConstraints == null)
        {
            return [];
        }

        return controllerActionDescriptor.ActionConstraints.OfType<HttpMethodActionConstraint>()
            .SelectMany(httpMethodActionConstraint => httpMethodActionConstraint.HttpMethods)
            .ToList();
    }

    private static bool Serves(ServerRoute serverRoute, ClientRoute clientRoute)
    {
        var isVerbAccepted = serverRoute.HttpMethods.Count == 0 ||
                             serverRoute.HttpMethods.Contains(clientRoute.HttpMethod, StringComparer.OrdinalIgnoreCase);
        if (!isVerbAccepted)
        {
            return false;
        }

        var clientPath = NormalizedPath(Segments(clientRoute.Path));
        return ServedPaths(serverRoute.Path).Contains(clientPath);
    }

    // The full path, and the path without each optional value at its end: {id?} serves orders/5 and orders.
    private static List<string> ServedPaths(string serverPath)
    {
        var segments = Segments(serverPath);
        var servedPaths = new List<string> { NormalizedPath(segments) };
        while (segments.Count > 0 && IsOptional(segments[^1]))
        {
            segments.RemoveAt(segments.Count - 1);
            servedPaths.Add(NormalizedPath(segments));
        }

        return servedPaths;
    }

    private static bool IsOptional(string segment)
    {
        return segment.StartsWith('{') && segment.EndsWith('}') && (segment.EndsWith("?}", StringComparison.Ordinal) || segment.Contains('='));
    }

    private static List<string> Segments(string path)
    {
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static string NormalizedPath(IEnumerable<string> segments)
    {
        return string.Join("/", segments.Select(segment => Placeholder.Replace(segment, "{}"))).ToLowerInvariant();
    }

    private static string CombinePath(string route, string template)
    {
        var trimmedRoute = route.Trim('/');
        var trimmedTemplate = template.Trim('/');
        if (trimmedTemplate.Length == 0)
        {
            return trimmedRoute;
        }

        return trimmedRoute + "/" + trimmedTemplate;
    }
}
