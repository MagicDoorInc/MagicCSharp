#!/usr/bin/env dotnet run
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:package Spectre.Console@0.50.0

// Rebuilds {Prefix}.All.slnx from every *.csproj on disk, preserving the two-level folder grouping
// (Apps/{Service}/, Libs/{Group}/) and the bare parent folders (/Apps/, /Libs/).
//
// Idempotent: an already-current solution is left untouched, so it produces no diff and no needless
// file-watcher churn. CreateApp, CreateAppLib and CreateLib run it automatically; run it by hand after a
// rebase or merge leaves the solution file conflicted.

using System.Text;
using System.Text.Json;
using Spectre.Console;

var config = RepoConfig.Load();
if (config == null)
{
    return 1;
}

var solutionFile = $"{config.Prefix}.All.slnx";

var enumeration = new EnumerationOptions
{
    RecurseSubdirectories = true,
    IgnoreInaccessible = true,
    AttributesToSkip = FileAttributes.System,
};

var projects = Directory.GetFiles(".", "*.csproj", enumeration)
    .Select(path => Path.GetRelativePath(".", path).Replace('\\', '/'))
    .Where(path => !path.Split('/').Any(segment => segment.StartsWith('.') || segment is "bin" or "obj" or "tools"))
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (projects.Count == 0)
{
    AnsiConsole.MarkupLine($"[yellow]No projects found — leaving {Markup.Escape(solutionFile)} unchanged.[/]");
    return 1;
}

var builder = new StringBuilder();
builder.AppendLine("<Solution>");

var emittedParents = new HashSet<string>();

// Group on the first two path segments, so Apps/Shop and Libs/Events each become a folder. A project
// sitting one level down falls back to its single segment.
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
        builder.AppendLine($"  <Folder Name=\"/{parent}/\" />");
    }

    builder.AppendLine($"  <Folder Name=\"/{group.Key}/\">");
    foreach (var project in group)
    {
        builder.AppendLine($"    <Project Path=\"{project}\" />");
    }

    builder.AppendLine("  </Folder>");
}

builder.AppendLine("</Solution>");

var content = builder.ToString();

if (File.Exists(solutionFile) && await File.ReadAllTextAsync(solutionFile) == content)
{
    AnsiConsole.MarkupLine($"[green]{Markup.Escape(solutionFile)} is up to date[/] ({projects.Count} projects).");
    return 0;
}

await File.WriteAllTextAsync(solutionFile, content);
AnsiConsole.MarkupLine($"[green]Rebuilt[/] {Markup.Escape(solutionFile)} with {projects.Count} projects.");

return 0;

/// <summary>
///     Reads magiccsharp.json from the working directory. Its presence is what marks a directory as the root
///     of a MagicCSharp-layout repository; every script refuses to run without it rather than guessing a
///     prefix and scattering files.
/// </summary>
internal record RepoConfig
{
    public const string FileName = "magiccsharp.json";

    /// <summary>Namespace and solution-name prefix, e.g. "Acme" gives Acme.Shop.slnx.</summary>
    public required string Prefix { get; init; }

    public static RepoConfig? Load()
    {
        if (!File.Exists(FileName))
        {
            AnsiConsole.MarkupLine($"[red]{FileName} not found.[/]");
            AnsiConsole.MarkupLine("[yellow]Run this from the repository root. Create the file with:[/]");
            AnsiConsole.WriteLine("""  { "prefix": "Acme" }""");
            return null;
        }

        var config = JsonSerializer.Deserialize<RepoConfig>(File.ReadAllText(FileName), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (config == null || string.IsNullOrWhiteSpace(config.Prefix))
        {
            AnsiConsole.MarkupLine($"[red]{FileName} has no \"prefix\".[/]");
            return null;
        }

        return config;
    }
}
