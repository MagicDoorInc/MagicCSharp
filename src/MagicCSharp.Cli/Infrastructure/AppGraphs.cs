namespace MagicCSharp.Cli.Infrastructure;

/// <summary>Reads every app in a repository into an <see cref="AppGraph" />.</summary>
public static class AppGraphs
{
    /// <summary>
    ///     One graph per app under <c>Apps/</c> that has a host project. With <paramref name="shouldIncludeSolution" />
    ///     off, an app depends on what its host project reaches — what ships, so what to deploy. With it on, it also
    ///     depends on everything in the app's own solution, tests included — so what to test.
    /// </summary>
    public static IReadOnlyList<AppGraph> Load(string repositoryRoot, string prefix, bool shouldIncludeSolution)
    {
        var appsFolder = Path.Combine(repositoryRoot, "Apps");
        if (!Directory.Exists(appsFolder))
        {
            return [];
        }

        var appGraphs = new List<AppGraph>();

        foreach (var appFolder in Directory.GetDirectories(appsFolder).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(appFolder);
            var hostFolder = Path.Combine(appFolder, $"{name}.App");
            var hostProject = Directory.Exists(hostFolder) ? Directory.GetFiles(hostFolder, "*.csproj").FirstOrDefault() : null;

            if (hostProject == null)
            {
                continue;
            }

            var folders = new List<string> { $"Apps/{name}" };
            folders.AddRange(ProjectGraph.Folders(repositoryRoot, hostProject));

            if (shouldIncludeSolution)
            {
                var solutionFile = $"{prefix}.{name}.slnx";
                folders.Add(solutionFile);

                foreach (var project in SolutionFile.ReadProjects(Path.Combine(repositoryRoot, solutionFile)))
                {
                    folders.AddRange(ProjectGraph.Folders(repositoryRoot, Path.Combine(repositoryRoot, project)));
                }
            }

            appGraphs.Add(new AppGraph
            {
                Name = name,
                Folders = folders.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            });
        }

        return appGraphs;
    }
}
