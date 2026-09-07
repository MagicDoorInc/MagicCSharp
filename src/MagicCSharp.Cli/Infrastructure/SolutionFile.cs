using System.Text;
using System.Text.RegularExpressions;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Reads and writes <c>.slnx</c> files.
///     <para>
///         Regenerated rather than edited in place, and written only when the content actually differs, so
///         re-running a command produces no diff and a conflicted solution file can be fixed by regenerating
///         rather than by resolving.
///     </para>
/// </summary>
public static partial class SolutionFile
{
    /// <summary>The project paths a solution lists.</summary>
    public static IReadOnlyList<string> ReadProjects(string solutionPath)
    {
        if (!File.Exists(solutionPath))
        {
            return [];
        }

        return ProjectPath().Matches(File.ReadAllText(solutionPath))
            .Select(match => match.Groups[1].Value)
            .ToList();
    }

    /// <summary>
    ///     Adds projects to a flat solution, keeping what is already there. Returns whether anything changed.
    /// </summary>
    public static bool AddProjects(string solutionPath, IEnumerable<string> projects)
    {
        var all = ReadProjects(solutionPath)
            .Concat(projects)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        var builder = new StringBuilder("<Solution>\n");
        foreach (var project in all)
        {
            builder.Append($"  <Project Path=\"{project}\" />\n");
        }

        builder.Append("</Solution>\n");

        return WriteIfChanged(solutionPath, builder.ToString());
    }

    /// <summary>
    ///     Rebuilds the all-projects solution from what is on disk, grouped by the first two path segments
    ///     so Apps/Shop and Libs/Events each become a folder.
    /// </summary>
    public static bool WriteGrouped(string solutionPath, IReadOnlyList<string> projects)
    {
        var builder = new StringBuilder("<Solution>\n");
        var emittedParents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var groups = projects
            .GroupBy(path =>
            {
                var segments = path.Split('/');
                return segments.Length >= 2 ? $"{segments[0]}/{segments[1]}" : segments[0];
            })
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var parent = group.Key.Split('/')[0];
            if (emittedParents.Add(parent))
            {
                builder.Append($"  <Folder Name=\"/{parent}/\" />\n");
            }

            builder.Append($"  <Folder Name=\"/{group.Key}/\">\n");
            foreach (var project in group)
            {
                builder.Append($"    <Project Path=\"{project}\" />\n");
            }

            builder.Append("  </Folder>\n");
        }

        builder.Append("</Solution>\n");

        return WriteIfChanged(solutionPath, builder.ToString());
    }

    /// <summary>
    ///     Every project on disk, excluding build output, hidden directories, and the tool's own sources.
    /// </summary>
    public static IReadOnlyList<string> DiscoverProjects(string root)
    {
        return Directory.EnumerateFiles(root, "*.csproj", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System,
            })
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Split('/').Any(segment =>
                segment.StartsWith('.') || segment is "bin" or "obj"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && File.ReadAllText(path) == content)
        {
            return false;
        }

        File.WriteAllText(path, content);
        return true;
    }

    [GeneratedRegex("Path=\"([^\"]+)\"")]
    private static partial Regex ProjectPath();
}
