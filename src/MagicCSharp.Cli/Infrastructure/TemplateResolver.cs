using System.Reflection;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Finds a template, letting a team override any single one without forking the rest.
///     <para>
///         A repository's own override directory is searched first; anything it does not provide comes from
///         the templates embedded in this tool. Resolution is per file, so overriding
///         <c>Entities/dal.cs.hbs</c> leaves every other template built-in and still tracking upstream.
///     </para>
/// </summary>
public class TemplateResolver(string? overrideDirectory)
{
    /// <summary>Where a repository keeps its overrides unless magiccsharp.json says otherwise.</summary>
    public const string DefaultOverrideDirectory = ".magiccsharp/templates";

    private const string ResourcePrefix = "Templates/";

    private static readonly Assembly Assembly = typeof(TemplateResolver).Assembly;

    /// <summary>Null when this repository has overrides turned off.</summary>
    public string? OverrideDirectory { get; } = string.IsNullOrWhiteSpace(overrideDirectory) ? null : overrideDirectory;

    /// <summary>
    ///     Builds a resolver from the repository config, falling back to the default directory when the
    ///     config does not mention one. Reading the file directly rather than through
    ///     <see cref="RepoConfig" /> lets <c>init</c> resolve templates before the config exists.
    /// </summary>
    public static TemplateResolver ForRepository(RepoConfig? config)
    {
        return new TemplateResolver(config == null ? DefaultOverrideDirectory : config.Templates ?? DefaultOverrideDirectory);
    }

    /// <summary>
    ///     The content of a template, e.g. "Entities/dal.cs.hbs".
    /// </summary>
    /// <exception cref="TemplateNotFoundException">Neither the override directory nor the built-ins have it.</exception>
    public string Read(string name)
    {
        var overridden = OverridePath(name);
        if (overridden != null && File.Exists(overridden))
        {
            return File.ReadAllText(overridden);
        }

        return ReadBuiltIn(name) ?? throw new TemplateNotFoundException(name);
    }

    /// <summary>Where a template comes from, for reporting.</summary>
    public TemplateSource Describe(string name)
    {
        var overridden = OverridePath(name);

        return overridden != null && File.Exists(overridden)
            ? new TemplateSource(name, overridden, true)
            : new TemplateSource(name, "built-in", false);
    }

    /// <summary>The path an override for this template would live at, or null when overrides are off.</summary>
    public string? OverridePath(string name)
    {
        return OverrideDirectory == null
            ? null
            : Path.Combine(OverrideDirectory, name.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>The built-in content, or null when there is no such template.</summary>
    public static string? ReadBuiltIn(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(ResourcePrefix + name);

        if (stream == null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Every template name this tool ships, in sorted order.</summary>
    public static IReadOnlyList<string> BuiltInNames()
    {
        return Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(name => name[ResourcePrefix.Length..])
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    ///     Every template, built-in or overridden. An override for a name with no built-in is still listed —
    ///     it is almost certainly a typo, and hiding it would make that hard to see.
    /// </summary>
    public IReadOnlyList<TemplateSource> All()
    {
        var names = new SortedSet<string>(BuiltInNames(), StringComparer.OrdinalIgnoreCase);

        if (OverrideDirectory != null && Directory.Exists(OverrideDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(OverrideDirectory, "*.hbs", SearchOption.AllDirectories))
            {
                names.Add(Path.GetRelativePath(OverrideDirectory, file).Replace('\\', '/'));
            }
        }

        return names.Select(Describe).ToList();
    }
}

/// <param name="Name">The template's name, e.g. "Entities/dal.cs.hbs".</param>
/// <param name="Location">The override's path, or "built-in".</param>
/// <param name="IsOverride">Whether the repository provides it.</param>
public record TemplateSource(string Name, string Location, bool IsOverride);

public class TemplateNotFoundException(string name)
    : Exception($"No template named '{name}'. Run 'mcs templates list' to see the names.")
{
    public string TemplateName { get; } = name;
}
