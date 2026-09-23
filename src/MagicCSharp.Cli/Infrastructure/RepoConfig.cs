using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     <c>magiccsharp.json</c> at the repository root. Its presence is what marks a directory as a
///     MagicCSharp-layout repository; the commands refuse to run without it rather than guessing a prefix
///     and scattering files into the wrong places.
/// </summary>
public record RepoConfig
{
    public const string FileName = "magiccsharp.json";

    /// <summary>Namespace and solution-name root. "Acme" gives Acme.Shop.slnx.</summary>
    [JsonPropertyName("prefix")]
    public required string Prefix { get; init; }

    /// <summary>
    ///     Where this repository keeps template overrides. Absent means the default; an empty string turns
    ///     overrides off.
    /// </summary>
    [JsonPropertyName("templates")]
    public string? Templates { get; init; }

    /// <summary>Reads the config, or null with a printed explanation when it is missing or unusable.</summary>
    public static RepoConfig? Load(string? directory = null)
    {
        var path = Path.Combine(directory ?? Directory.GetCurrentDirectory(), FileName);

        if (!File.Exists(path))
        {
            Output.Error($"{FileName} not found.");
            Output.Hint("Run this from the repository root, or set one up with:");
            Output.Plain("  mcs init --prefix Acme");
            return null;
        }

        return Read(path);
    }

    /// <summary>
    ///     Reads the config if there is one, saying nothing when there is not.
    ///     <para>
    ///         For commands that work outside a repository. The template commands only need the config to
    ///         find the override directory, which has a default — so a missing config is an ordinary case,
    ///         not an error worth a red line. It is what lets you build a shared template repository in an
    ///         empty directory.
    ///     </para>
    /// </summary>
    public static RepoConfig? TryLoad(string? directory = null)
    {
        var path = Path.Combine(directory ?? Directory.GetCurrentDirectory(), FileName);

        return File.Exists(path) ? Read(path) : null;
    }

    private static RepoConfig? Read(string path)
    {

        RepoConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<RepoConfig>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            Output.Error($"{FileName} is not valid JSON: {ex.Message}");
            return null;
        }

        if (config == null || string.IsNullOrWhiteSpace(config.Prefix))
        {
            Output.Error($"{FileName} has no \"prefix\".");
            return null;
        }

        return config;
    }

    /// <summary>
    ///     The service name a solution file belongs to: "Acme.Shop.slnx" with prefix "Acme" gives "Shop".
    /// </summary>
    public string AppNameFromSolution(string solutionPath)
    {
        return Path.GetFileName(solutionPath)
            .Replace($"{Prefix}.", "", StringComparison.Ordinal)
            .Replace(".slnx", "", StringComparison.Ordinal);
    }
}
