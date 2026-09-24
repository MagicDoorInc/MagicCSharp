namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Which apps a set of changed files affects — the question a CI pipeline asks so it builds, tests and
///     deploys only what changed.
///     <para>
///         An app is affected when a changed file is inside its own folder under <c>Apps/</c>, inside the folder of
///         any project it depends on (a shared library under <c>Libs/</c>, say), or is a file every project builds
///         with — central package versions, shared build properties, the build rules' configuration.
///     </para>
/// </summary>
public static class AffectedApps
{
    /// <summary>A change to any of these can change how every project builds or what every image copies, so it affects every app.</summary>
    public static readonly IReadOnlyList<string> RepositoryWideFiles =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "global.json",
        "nuget.config",
        "NuGet.Config",
        ".editorconfig",
        ".dockerignore",
        RepositoryConfig.FileName,
    ];

    public static IReadOnlyList<string> Find(IReadOnlyList<AppGraph> apps, IReadOnlyList<string> changedPaths)
    {
        var normalizedPaths = changedPaths.Select(path => path.Replace('\\', '/').Trim()).Where(path => path.Length > 0).ToList();

        var isRepositoryWide = normalizedPaths.Any(path => RepositoryWideFiles.Contains(path, StringComparer.Ordinal));
        if (isRepositoryWide)
        {
            return apps.Select(app => app.Name).Order(StringComparer.Ordinal).ToList();
        }

        return apps
            .Where(app => normalizedPaths.Any(path => IsInsideAny(path, app.Folders)))
            .Select(app => app.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsInsideAny(string path, IReadOnlyList<string> folders)
    {
        return folders.Any(folder => path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal));
    }
}
