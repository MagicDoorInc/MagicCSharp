using System.Reflection;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     The guides for AI coding agents that mcs ships: <c>AGENTS.md</c> for every agent, <c>CLAUDE.md</c> importing it
///     for Claude Code, and <c>.ai-knowledge/</c>. Their source is
///     the <c>AIAgents/</c> folder at the root of the MagicCSharp repository, embedded into this tool at build time,
///     so there is one place to edit them and every repository gets the version its mcs ships.
///     <para>
///         <c>init</c> writes whatever is missing. <c>update ai-files</c> overwrites the files mcs ships with that
///         version — except <see cref="ProjectFile" />, which is written once and then belongs to the repository,
///         and any file mcs does not ship, which it never touches.
///     </para>
/// </summary>
public static class AiFiles
{
    /// <summary>The one shipped file the repository owns after it is created: its own services and exceptions.</summary>
    public const string ProjectFile = ".ai-knowledge/project.md";

    private const string ResourcePrefix = "AIAgents/";

    /// <summary>The guides mcs ships, each with the repository prefix filled in, keyed by its path in a repository.</summary>
    public static IReadOnlyList<AiFile> Bundled(string prefix)
    {
        var assembly = typeof(AiFiles).Assembly;

        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => new AiFile
            {
                Path = name[ResourcePrefix.Length..],
                Content = ReadResource(assembly, name).Replace("{{ prefix }}", prefix),
            })
            .ToList();
    }

    /// <summary>Writes every shipped guide that is missing under <paramref name="root" />. Returns whether it wrote any.</summary>
    public static bool WriteMissing(string root, string prefix)
    {
        var hasWritten = false;

        foreach (var aiFile in Bundled(prefix))
        {
            hasWritten |= TemplateRenderer.Write(Path.Combine(root, aiFile.Path), aiFile.Content);
        }

        return hasWritten;
    }

    /// <summary>
    ///     Brings the shipped guides under <paramref name="root" /> to this version. Returns how many files it
    ///     created or changed.
    /// </summary>
    public static int Update(string root, string prefix)
    {
        var changedCount = 0;

        foreach (var aiFile in Bundled(prefix))
        {
            var targetPath = Path.Combine(root, aiFile.Path);

            if (aiFile.Path == ProjectFile)
            {
                changedCount += TemplateRenderer.Write(targetPath, aiFile.Content) ? 1 : 0;
                continue;
            }

            if (!File.Exists(targetPath))
            {
                changedCount += TemplateRenderer.Write(targetPath, aiFile.Content) ? 1 : 0;
                continue;
            }

            if (File.ReadAllText(targetPath) == aiFile.Content)
            {
                continue;
            }

            File.WriteAllText(targetPath, aiFile.Content);
            Output.Updated(targetPath, "to this mcs version");
            changedCount++;
        }

        return changedCount;
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}

/// <summary>One shipped guide: where it goes in a repository, and what it says.</summary>
public record AiFile
{
    public required string Path { get; init; }
    public required string Content { get; init; }
}
