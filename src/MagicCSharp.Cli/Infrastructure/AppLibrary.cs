namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Works out where a library inside a service goes, and what it is called.
///     <para>
///         One rule does the work: dots in the name nest directories, and the leaf holds up to three
///         projects — <c>Default</c>, <c>Models</c> and <c>Tests</c>. The first segment is glued to the
///         service name, so <c>Domains.Orders</c> in service Shop is
///         <c>Apps/Shop/Shop.Domains/Orders</c>, and every further segment is a subdirectory.
///     </para>
///     <para>
///         Everything else the commands do is derived from the name here rather than decided by a flag:
///         whether the host must reference it, whether it has a parent, and whether it is a domain's HTTP
///         surface. Kept apart from the command so the rules can be tested without touching a disk.
///     </para>
/// </summary>
public record AppLibrary
{
    private AppLibrary()
    {
    }

    /// <summary>The dotted name, after any command-specific prefix has been applied.</summary>
    public required string Name { get; init; }

    /// <summary>The service this belongs to, e.g. "Shop".</summary>
    public required string AppName { get; init; }

    /// <summary>Where the library's projects live, e.g. "Apps/Shop/Shop.Domains/Orders".</summary>
    public required string Directory { get; init; }

    /// <summary>Assembly and root namespace of the Default project, e.g. "Acme.Shop.Domains.Orders".</summary>
    public required string AssemblyName { get; init; }

    public string DefaultProject => $"{Directory}/Default/{AssemblyName}.csproj";

    public string ModelsProject => $"{Directory}/Models/{AssemblyName}.Models.csproj";

    public string TestsProject => $"{Directory}/Tests/{AssemblyName}.Tests.csproj";

    /// <summary>The service's host project, which must reference every domain.</summary>
    public required string HostProject { get; init; }

    /// <summary>
    ///     Under <c>{App}.Domains/</c>, so the host has to reference it — an assembly nothing references is
    ///     not deployed, and its use cases and event handlers are never discovered.
    /// </summary>
    public required bool IsDomain { get; init; }

    /// <summary>
    ///     The last segment is <c>App</c>: this is the parent's HTTP surface, holding its controllers and
    ///     the request and response types they use.
    /// </summary>
    public required bool IsHttpSurface { get; init; }

    /// <summary>
    ///     The enclosing library's Default project when this name is nested, whether or not it exists yet.
    ///     Null for a single-segment name.
    /// </summary>
    public string? ParentDefaultProject { get; init; }

    /// <summary>The template the Default project is rendered from.</summary>
    public string DefaultTemplate => IsHttpSurface ? "Libraries/app.csproj.hbs" : "Libraries/default.csproj.hbs";

    /// <summary>
    ///     The name to pass to <c>add-entity --domain</c>, or null when entities do not belong here.
    /// </summary>
    public string? EntityDomain
    {
        get
        {
            if (!IsDomain || IsHttpSurface)
            {
                return null;
            }

            return string.Join('.', Name.Split('.').Skip(1));
        }
    }

    /// <summary>
    ///     Plans a library, or explains why the name will not do. Validation is here rather than in the
    ///     command's <c>Validate</c> because the rules need the service name.
    /// </summary>
    public static AppLibrary? Plan(RepoConfig config, string appName, string name)
    {
        var segments = name.Split('.');

        if (!Naming.IsDottedPascal(name))
        {
            Output.Error($"Name must be PascalCase segments separated by dots: {name}");
            return null;
        }

        if (!Validate(segments))
        {
            return null;
        }

        var appRoot = $"Apps/{appName}";

        // Domains.Orders -> Shop.Domains/Orders. The first segment joins the service name, so the
        // directory reads as the assembly does.
        var directory = $"{appRoot}/{appName}.{segments[0]}";

        if (segments.Length > 1)
        {
            directory += "/" + string.Join('/', segments.Skip(1));
        }

        var assemblyName = $"{config.Prefix}.{appName}.{name}";

        return new AppLibrary
        {
            Name = name,
            AppName = appName,
            Directory = directory,
            AssemblyName = assemblyName,
            HostProject = $"{appRoot}/{appName}.App/{config.Prefix}.{appName}.App.csproj",
            IsDomain = segments[0] == "Domains",
            IsHttpSurface = segments.Length > 1 && segments[^1] == "App",
            ParentDefaultProject = ParentOf(config, appName, segments),
        };
    }

    private static string? ParentOf(RepoConfig config, string appName, string[] segments)
    {
        if (segments.Length < 2)
        {
            return null;
        }

        var parent = segments[..^1];
        var directory = $"Apps/{appName}/{appName}.{parent[0]}";

        if (parent.Length > 1)
        {
            directory += "/" + string.Join('/', parent.Skip(1));
        }

        return $"{directory}/Default/{config.Prefix}.{appName}.{string.Join('.', parent)}.csproj";
    }

    private static bool Validate(string[] segments)
    {
        foreach (var segment in segments)
        {
            // These are the leaf project directories. A library called Orders.Models would want
            // Orders/Models/Default beside the Models project of Orders itself.
            if (segment is "Default" or "Models" or "Tests")
            {
                Output.Error($"'{segment}' is one of the three project directories, so it cannot also be part of the name.");
                return false;
            }
        }

        if (segments[0] == "App")
        {
            Output.Error("The first segment cannot be App — that is the service's host project.");
            Output.Hint("For a domain's endpoints, name it after the domain: Orders.App");
            return false;
        }

        if (segments[0] == "Data")
        {
            Output.Error("The first segment cannot be Data.");
            Output.Hint($"Persistence is one place per service: repository interfaces in Data/Data.Models,");
            Output.Plain("  DALs, repositories, the context and migrations in Data/Data.EntityFramework.");
            return false;
        }

        if (segments is ["Domains"])
        {
            Output.Error("Domains is the container, not a library. Name the domain itself:");
            Output.Hint("  mcs create-domain --name Orders");
            return false;
        }

        // App is the parent's HTTP surface, so there has to be a parent for it to be the surface of.
        if (segments.Length == 1 && segments[0] == "App")
        {
            return false;
        }

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i] == "App")
            {
                Output.Error("App may only be the last segment — it is the HTTP surface of what precedes it.");
                return false;
            }
        }

        return true;
    }
}
