using System.Xml.Linq;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Which projects a project depends on, directly or through others, read from the <c>ProjectReference</c>
///     items in the .csproj files themselves. Reading the XML rather than running <c>dotnet list reference</c>
///     per project keeps a whole repository's graph to milliseconds, which matters when CI asks for every app.
/// </summary>
public static class ProjectGraph
{
    /// <summary>
    ///     The project and every project it reaches through <c>ProjectReference</c>, as full paths. Fails on a
    ///     reference to a file that does not exist, because a graph with a hole in it would under-report what a
    ///     change affects.
    /// </summary>
    public static IReadOnlyList<string> Closure(string projectPath)
    {
        var startPath = Path.GetFullPath(projectPath);
        if (!File.Exists(startPath))
        {
            throw new FileNotFoundException($"Project not found: {projectPath}");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal) { startPath };
        var ordered = new List<string> { startPath };
        var queue = new Queue<string>();
        queue.Enqueue(startPath);

        while (queue.Count > 0)
        {
            var currentPath = queue.Dequeue();

            foreach (var referencedPath in References(currentPath))
            {
                if (!File.Exists(referencedPath))
                {
                    throw new FileNotFoundException($"{currentPath} references a project that does not exist: {referencedPath}");
                }

                if (seen.Add(referencedPath))
                {
                    ordered.Add(referencedPath);
                    queue.Enqueue(referencedPath);
                }
            }
        }

        return ordered;
    }

    /// <summary>
    ///     The folders of <see cref="Closure" />, relative to <paramref name="repositoryRoot" /> with forward
    ///     slashes, sorted — the form a changed-files list from git can be compared against.
    /// </summary>
    public static IReadOnlyList<string> Folders(string repositoryRoot, string projectPath)
    {
        return Closure(projectPath)
            .Select(path => ToRelativeFolder(repositoryRoot, path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public static string ToRelativeFolder(string repositoryRoot, string projectPath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        return Path.GetRelativePath(Path.GetFullPath(repositoryRoot), folder).Replace('\\', '/');
    }

    private static IEnumerable<string> References(string projectPath)
    {
        var projectFolder = Path.GetDirectoryName(projectPath)!;

        return XDocument.Load(projectPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(projectFolder, include!.Replace('\\', Path.DirectorySeparatorChar))));
    }
}
