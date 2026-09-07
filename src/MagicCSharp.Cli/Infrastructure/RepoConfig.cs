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
