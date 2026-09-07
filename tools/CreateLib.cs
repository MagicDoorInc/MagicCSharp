#!/usr/bin/env dotnet run
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:package Spectre.Console.Cli@0.50.0
#:package Scriban@7.2.5

// Creates a shared library under Libs/ — code used by more than one service.
//
// A dotted name nests: --name Clients.Billing lands in Libs/Clients/Billing/, which is what keeps a dozen
// client libraries from sitting flat next to a dozen unrelated ones.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scriban;
using Scriban.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<CreateLibCommand>();
app.Configure(config =>
{
    config.SetApplicationName("dotnet run tools/CreateLib.cs --");
    config.AddExample("--name", "Events");
    config.AddExample("--name", "Clients.Billing", "--tests");
});
return app.Run(args);

public class CreateLibSettings : CommandSettings
{
    [CommandOption("-n|--name <NAME>")]
    [Description("Library name; dots nest directories, e.g. 'Clients.Billing'")]
    public string? Name { get; set; }

    [CommandOption("-t|--tests")]
    [Description("Also create a Tests project alongside it")]
    [DefaultValue(false)]
    public bool IncludeTests { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return ValidationResult.Error("Library name is required. Use --name <NAME>");
        }

        return Regex.IsMatch(Name, @"^[A-Z][0-9a-zA-Z]*(\.[A-Z][0-9a-zA-Z]*)*$")
            ? ValidationResult.Success()
            : ValidationResult.Error($"Name must be PascalCase segments separated by dots: {Name}");
    }
}

public class CreateLibCommand : AsyncCommand<CreateLibSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreateLibSettings settings)
    {
        var config = RepoConfig.Load();
        if (config == null)
        {
            return 1;
        }

        var name = settings.Name!;
        var directory = $"Libs/{name.Replace('.', '/')}";
        var assemblyName = $"{config.Prefix}.Libraries.{name}";

        var model = new
        {
            prefix = config.Prefix,
            name,
            assembly_name = assemblyName,
        };

        AnsiConsole.MarkupLine($"[green]Library:[/] {assemblyName}");
        AnsiConsole.WriteLine();

        var created = await Scaffold.Render("Libraries/default.csproj.hbs", $"{directory}/Default/{assemblyName}.csproj", model);

        if (settings.IncludeTests)
        {
            created |= await Scaffold.Render("Libraries/tests.csproj.hbs", $"{directory}/Tests/{assemblyName}.Tests.csproj", model);
        }

        if (created)
        {
            await Scaffold.SyncSolution();
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(created
            ? $"[bold green]Done.[/] Add it to a service with: dotnet add <project> reference {Markup.Escape(directory)}/Default/{Markup.Escape(assemblyName)}.csproj"
            : "[blue]Nothing to do — everything requested already exists.[/]");

        return 0;
    }
}

public static class Scaffold
{
    /// <summary>
    ///     Renders a template to a path, never overwriting. Returns whether it wrote anything, so a caller can
    ///     skip follow-up work when a re-run had nothing to do.
    /// </summary>
    public static async Task<bool> Render(string templateName, string targetPath, object model)
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

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllTextAsync(targetPath, await template.RenderAsync(templateContext));

        AnsiConsole.MarkupLine($"  [green]created[/] {Markup.Escape(targetPath)}");
        return true;
    }

    /// <summary>Rebuilds the all-projects solution so a new project is in it without a second command.</summary>
    public static async Task SyncSolution()
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("run");
        info.ArgumentList.Add("tools/SyncAllProjects.cs");

        using var process = Process.Start(info)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(line.Trim())}[/]");
        }
    }
}

/// <summary>
///     Reads magiccsharp.json from the working directory. Its presence is what marks a directory as the root
///     of a MagicCSharp-layout repository.
/// </summary>
public record RepoConfig
{
    public const string FileName = "magiccsharp.json";

    public required string Prefix { get; init; }

    public static RepoConfig? Load()
    {
        if (!File.Exists(FileName))
        {
            AnsiConsole.MarkupLine($"[red]{FileName} not found.[/]");
            AnsiConsole.MarkupLine("[yellow]Run this from the repository root, or set it up with:[/]");
            AnsiConsole.WriteLine("  dotnet run tools/InitRepo.cs -- --prefix Acme");
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
