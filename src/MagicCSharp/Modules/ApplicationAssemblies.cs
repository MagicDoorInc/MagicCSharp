using System.Reflection;

namespace MagicCSharp.Modules;

/// <summary>
///     Finds the assemblies that make up the running application, loading the ones .NET has not got round
///     to yet.
///     <para>
///         This exists because discovery by reflection over <see cref="AppDomain.CurrentDomain" /> quietly
///         does the wrong thing. .NET loads an assembly the first time one of its types is touched, so a
///         project the host references but has not used yet is simply not there when the scan runs. A
///         domain project containing only event handlers is the usual casualty: it builds, it ships, it is
///         referenced, and its handlers never run. Nothing fails — the dispatch returns and no handler
///         was registered to receive it.
///     </para>
///     <para>
///         Walking the reference graph from the entry assembly forces each one in before the scan, so what
///         gets discovered depends on what the application references rather than on what it happened to
///         touch first.
///     </para>
/// </summary>
public static class ApplicationAssemblies
{
    /// <summary>
    ///     Assemblies whose references are not worth following. These are the framework and the BCL: they
    ///     never contain application types, and loading the graph below them costs startup time for nothing.
    /// </summary>
    private static readonly string[] SystemPrefixes =
    [
        "System",
        "Microsoft",
        "netstandard",
        "mscorlib",
        "WindowsBase",
        "Newtonsoft",
        "xunit",
        "testhost",
        "Anonymously Hosted DynamicMethods Assembly",
    ];

    /// <summary>
    ///     Every application assembly, loaded and ready to scan.
    /// </summary>
    /// <param name="filter">
    ///     Narrows what comes back. Applied after loading, so a narrow filter still gets a complete graph
    ///     to choose from. Defaults to "not a framework assembly".
    /// </param>
    public static IReadOnlyList<Assembly> All(Func<Assembly, bool>? filter = null)
    {
        EnsureLoaded();

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Where(filter ?? IsApplicationAssembly)
            .ToList();
    }

    /// <summary>
    ///     True for anything that is not part of the framework — the default notion of "your code".
    /// </summary>
    public static bool IsApplicationAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name;

        return name != null && !IsSystem(name);
    }

    /// <summary>Set once the deployment directory has been read, so repeat calls cost nothing.</summary>
    private static bool loaded;

    private static readonly Lock Gate = new Lock();

    /// <summary>
    ///     Loads every application assembly deployed with the executable, so a later scan sees all of it.
    ///     <para>
    ///         Safe to call repeatedly, and cheap after the first time. Registration methods call this
    ///         themselves; you need it directly only if you are scanning by reflection yourself.
    ///     </para>
    /// </summary>
    public static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        lock (Gate)
        {
            if (loaded)
            {
                return;
            }

            foreach (var path in DeployedAssemblies())
            {
                // One assembly that will not load is not this scan's problem — it will surface properly
                // the moment something actually needs a type from it. Carry on with the rest.
                TryLoadFrom(path);
            }

            loaded = true;
        }
    }

    /// <summary>
    ///     The managed DLLs sitting next to the executable, minus the framework. Under a single-file
    ///     publish there is nothing beside the executable to find, and this yields nothing — that layout
    ///     has everything bundled and already loaded.
    /// </summary>
    private static IEnumerable<string> DeployedAssemblies()
    {
        var directory = AppContext.BaseDirectory;

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
                .Where(path => !IsSystem(Path.GetFileNameWithoutExtension(path)))
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static void TryLoadFrom(string path)
    {
        try
        {
            // Resolves to the already-loaded assembly when identities match, so this does not produce a
            // second copy of anything.
            Assembly.LoadFrom(path);
        }
        catch (BadImageFormatException)
        {
            // A native library that happens to end in .dll. Nothing to scan in it.
        }
        catch (Exception)
        {
            // Missing dependency, locked file, or something the host will not permit. Not fatal here.
        }
    }

    private static bool IsSystem(string name)
    {
        return SystemPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
