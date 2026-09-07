#!/usr/bin/env dotnet run
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:package Spectre.Console.Cli@0.50.0
#:package Scriban@7.2.5

// Turns a directory into a repository the other tools understand.
//
// Everything it writes is ordinary, editable configuration — there is no runtime component and nothing reads
// these files but MSBuild and the tools here. Nothing is overwritten: run it in an existing repository to add
// only the pieces that are missing.

using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scriban;
using Scriban.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<InitRepoCommand>();
app.Configure(config =>
{
    config.SetApplicationName("dotnet run tools/InitRepo.cs --");
    config.AddExample("--prefix", "Acme");
    config.AddExample("--prefix", "Acme", "--package-version", "0.1.0");
});
return app.Run(args);

public class InitRepoSettings : CommandSettings
{
    [CommandOption("-p|--prefix <PREFIX>")]
    [Description("Namespace and solution-name root, e.g. 'Acme' gives Acme.Shop.slnx")]
    public string? Prefix { get; set; }

    [CommandOption("--package-version <VERSION>")]
    [Description("MagicCSharp version to pin in Directory.Packages.props")]
    [DefaultValue("0.1.0")]
    public string PackageVersion { get; set; } = "0.1.0";

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Prefix))
        {
            return ValidationResult.Error("Prefix is required. Use --prefix <PREFIX>");
        }

        if (!Regex.IsMatch(Prefix, "^[A-Z][0-9a-zA-Z]*$"))
        {
            return ValidationResult.Error($"Prefix must be PascalCase with no dots: {Prefix}");
        }

        return ValidationResult.Success();
    }
}

public class InitRepoCommand : AsyncCommand<InitRepoSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, InitRepoSettings settings)
    {
        var prefix = settings.Prefix!;
        var model = new { prefix, version = settings.PackageVersion };

        AnsiConsole.MarkupLine($"[green]Prefix:[/] {prefix}   [green]MagicCSharp:[/] {settings.PackageVersion}");
        AnsiConsole.WriteLine();

        var wrote = false;

        // "templates" is written explicitly, even though it matches the default, so the override
        // mechanism is discoverable from the config rather than only from documentation.
        wrote |= await WriteIfMissing("magiccsharp.json", $$"""
                                                           {
                                                             "prefix": "{{prefix}}",
                                                             "templates": "{{TemplateResolver.DefaultOverrideDirectory}}"
                                                           }

                                                           """);

        wrote |= await RenderIfMissing("Repo/Directory.Build.props.hbs", "Directory.Build.props", model);
        wrote |= await RenderIfMissing("Repo/Directory.Packages.props.hbs", "Directory.Packages.props", model);

        wrote |= await WriteIfMissing($"{prefix}.All.slnx", "<Solution>\n</Solution>\n");

        // Placeholders so the two top-level directories exist in a fresh clone; git does not track empty
        // directories, and a missing Libs/ makes CreateLib's first run look like it did something odd.
        wrote |= await WriteIfMissing("Apps/.gitkeep", "");
        wrote |= await WriteIfMissing("Libs/.gitkeep", "");

        AnsiConsole.WriteLine();

        if (!wrote)
        {
            AnsiConsole.MarkupLine("[blue]Nothing to do — this repository is already set up.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[bold green]Ready.[/] Next:");
        AnsiConsole.MarkupLine($"  dotnet run tools/CreateApp.cs -- --name Shop --database shop");
        AnsiConsole.MarkupLine($"  dotnet run tools/CreateAppLib.cs -- --solution {prefix}.Shop.slnx --name Domains.Orders --models --tests");
        AnsiConsole.MarkupLine($"  dotnet run tools/AddEntity.cs -- --solution {prefix}.Shop.slnx --domain Orders --name Order --paginated");

        return 0;
    }

    private static async Task<bool> WriteIfMissing(string path, string content)
    {
        if (File.Exists(path))
        {
            AnsiConsole.MarkupLine($"  [grey]exists, kept[/] {Markup.Escape(path)}");
            return false;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
        AnsiConsole.MarkupLine($"  [green]created[/] {Markup.Escape(path)}");
        return true;
    }

    private static async Task<bool> RenderIfMissing(string templateName, string targetPath, object model)
    {
        if (File.Exists(targetPath))
        {
            AnsiConsole.MarkupLine($"  [grey]exists, kept[/] {Markup.Escape(targetPath)}");
            return false;
        }

        var templatePath = TemplateResolver.Resolve(templateName);
        var template = Template.Parse(await File.ReadAllTextAsync(templatePath));

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        var templateContext = new TemplateContext();
        templateContext.PushGlobal(scriptObject);

        await File.WriteAllTextAsync(targetPath, await template.RenderAsync(templateContext));
        AnsiConsole.MarkupLine($"  [green]created[/] {Markup.Escape(targetPath)}");
        return true;
    }
}

/// <summary>
///     Finds a template, letting a team override any single one without forking the rest.
///     <para>
///         Looked up in order, first match winning: the repository's own override directory, then the
///         templates installed alongside the tools, then a vendored <c>tools/Templates</c>. Resolution is
///         per file, so overriding <c>Entities/dal.cs.hbs</c> leaves every other template built-in.
///     </para>
/// </summary>
public static class TemplateResolver
{
    /// <summary>Where a repository keeps its overrides unless magiccsharp.json says otherwise.</summary>
    public const string DefaultOverrideDirectory = ".magiccsharp/templates";

    /// <summary>
    ///     The full path of a template, e.g. "Entities/dal.cs.hbs".
    /// </summary>
    /// <exception cref="FileNotFoundException">No layer provides it.</exception>
    public static string Resolve(string relativePath)
    {
        var relative = relativePath.Replace('/', Path.DirectorySeparatorChar);

        foreach (var root in Roots())
        {
            var candidate = Path.Combine(root, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Template '{relativePath}' not found in: {string.Join(", ", Roots())}");
    }

    /// <summary>The layers, most specific first. Non-existent ones are still listed, for error messages.</summary>
    public static IReadOnlyList<string> Roots()
    {
        var roots = new List<string>();

        var overrides = OverrideDirectory();
        if (!string.IsNullOrWhiteSpace(overrides))
        {
            roots.Add(overrides);
        }

        // Set by the mcs dispatcher to the installed template directory.
        var installed = Environment.GetEnvironmentVariable("MAGICCSHARP_TEMPLATES_DIR");
        if (!string.IsNullOrWhiteSpace(installed))
        {
            roots.Add(installed);
        }

        // A repository that vendored the tools rather than installing them.
        roots.Add(Path.Combine("tools", "Templates"));

        return roots;
    }

    /// <summary>
    ///     The repository's override directory: the "templates" key of magiccsharp.json, or
    ///     <see cref="DefaultOverrideDirectory" /> when the key is absent. Read straight from the file rather
    ///     than through RepoConfig, so InitRepo can resolve templates before the config exists.
    /// </summary>
    public static string? OverrideDirectory()
    {
        if (!File.Exists("magiccsharp.json"))
        {
            return DefaultOverrideDirectory;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText("magiccsharp.json"));
            if (document.RootElement.TryGetProperty("templates", out var configured) &&
                configured.ValueKind == JsonValueKind.String)
            {
                var value = configured.GetString();
                // An explicit empty string opts out of overrides entirely.
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch (JsonException)
        {
            // A malformed config is RepoConfig's problem to report; template lookup just falls back.
        }

        return DefaultOverrideDirectory;
    }
}
