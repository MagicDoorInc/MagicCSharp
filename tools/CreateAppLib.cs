#!/usr/bin/env dotnet run
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:package Spectre.Console.Cli@0.50.0
#:package Scriban@7.2.5

// Creates a domain inside a service: Apps/{App}/{App}.Domains/{Domain}/.
//
// A domain is up to three projects. Default holds the use cases and event handlers. Models holds the
// entities, edits and filters — separate so the data projects can reference the entities without referencing
// the logic, which is what stops a repository from calling a use case. Tests holds the tests.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scriban;
using Scriban.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<CreateAppLibCommand>();
app.Configure(config =>
{
    config.SetApplicationName("dotnet run tools/CreateAppLib.cs --");
    config.AddExample("--solution", "Acme.Shop.slnx", "--name", "Domains.Orders", "--models", "--tests");
    config.AddExample("-s", "Acme.Shop.slnx", "-n", "Domains.Orders.App", "--tests");
});
return app.Run(args);

public class CreateAppLibSettings : CommandSettings
{
    [CommandOption("-s|--solution <SOLUTION>")]
    [Description("The service's solution file, e.g. 'Acme.Shop.slnx'")]
    public string? Solution { get; set; }

    [CommandOption("-n|--name <NAME>")]
    [Description("Library name, e.g. 'Domains.Orders'")]
    public string? Name { get; set; }

    [CommandOption("-m|--models")]
    [Description("Also create a Models project. AddEntity requires one.")]
    [DefaultValue(false)]
    public bool IncludeModels { get; set; }

    [CommandOption("-t|--tests")]
    [Description("Also create a Tests project")]
    [DefaultValue(false)]
    public bool IncludeTests { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Solution))
        {
            return ValidationResult.Error("Solution file is required. Use --solution <FILE>");
        }

        if (!File.Exists(Solution) || !Solution.EndsWith(".slnx"))
        {
            return ValidationResult.Error($"Solution file not found or not a .slnx: {Solution}");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            return ValidationResult.Error("Library name is required. Use --name <NAME>");
        }

        return Regex.IsMatch(Name, @"^[A-Z][0-9a-zA-Z]*(\.[A-Z][0-9a-zA-Z]*)*$")
            ? ValidationResult.Success()
            : ValidationResult.Error($"Name must be PascalCase segments separated by dots: {Name}");
    }
}

public class CreateAppLibCommand : AsyncCommand<CreateAppLibSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreateAppLibSettings settings)
    {
        var config = RepoConfig.Load();
        if (config == null)
        {
            return 1;
        }

        var solution = settings.Solution!;
        var name = settings.Name!;
        var prefix = config.Prefix;

        // "Acme.Shop.slnx" -> "Shop"
        var appName = Path.GetFileName(solution).Replace($"{prefix}.", "").Replace(".slnx", "");

        var appRoot = $"Apps/{appName}";
        if (!Directory.Exists(appRoot))
        {
            AnsiConsole.MarkupLine($"[red]Service not found:[/] {Markup.Escape(appRoot)}");
            AnsiConsole.MarkupLine($"[yellow]Create it first:[/] dotnet run tools/CreateApp.cs -- --name {Markup.Escape(appName)} --database {appName.ToLowerInvariant()}");
            return 1;
        }

        // Domains.Orders -> Domains/Orders; Domains.Orders.App -> Domains/Orders/App
        var directory = $"{appRoot}/{appName}.Domains/{string.Join('/', name.Split('.').Skip(1))}";
        var assemblyName = $"{prefix}.{appName}.{name}";

        var model = new
        {
            prefix,
            name,
            assembly_name = assemblyName,
        };

        AnsiConsole.MarkupLine($"[green]Service:[/] {appName}   [green]Library:[/] {assemblyName}");
        AnsiConsole.WriteLine();

        var projects = new List<string>();

        var defaultProject = $"{directory}/Default/{assemblyName}.csproj";
        await Scaffold.Render("Libraries/default.csproj.hbs", defaultProject, model);
        projects.Add(defaultProject);

        if (settings.IncludeModels)
        {
            var modelsProject = $"{directory}/Models/{assemblyName}.Models.csproj";
            await Scaffold.Render("Libraries/models.csproj.hbs", modelsProject, model);
            projects.Add(modelsProject);

            // The use cases work with the entities, so Default depends on Models. The reverse must never
            // happen: Models is what the data projects reference, and a cycle would follow.
            await Dotnet.EnsureReference(defaultProject, modelsProject);
        }

        if (settings.IncludeTests)
        {
            var testsProject = $"{directory}/Tests/{assemblyName}.Tests.csproj";
            await Scaffold.Render("Libraries/tests.csproj.hbs", testsProject, model);
            projects.Add(testsProject);
        }

        await AddToServiceSolution(solution, projects);
        await Scaffold.SyncSolution();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]Done.[/]");

        if (settings.IncludeModels)
        {
            var domain = name.Split('.').Last();
            AnsiConsole.MarkupLine(
                $"  dotnet run tools/AddEntity.cs -- --solution {Markup.Escape(solution)} --domain {Markup.Escape(domain)} --name YourEntity --paginated");
        }
        else
        {
            AnsiConsole.MarkupLine("  [grey]Pass --models if you want AddEntity to be able to place entities here.[/]");
        }

        return 0;
    }

    private static async Task AddToServiceSolution(string solutionFile, IReadOnlyList<string> projects)
    {
        var existing = Regex.Matches(await File.ReadAllTextAsync(solutionFile), @"Path=""([^""]+)""").Select(match => match.Groups[1].Value);

        var all = existing.Concat(projects).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        var content = "<Solution>\n" + string.Join("", all.Select(path => $"  <Project Path=\"{path}\" />\n")) + "</Solution>\n";

        if (await File.ReadAllTextAsync(solutionFile) == content)
        {
            AnsiConsole.MarkupLine($"  [grey]up to date[/] {Markup.Escape(solutionFile)}");
            return;
        }

        await File.WriteAllTextAsync(solutionFile, content);
        AnsiConsole.MarkupLine($"  [green]written[/] {Markup.Escape(solutionFile)}");
    }
}

public static class Scaffold
{
    /// <summary>Renders a template to a path, never overwriting.</summary>
    public static async Task<bool> Render(string templateName, string targetPath, object model)
    {
        if (File.Exists(targetPath))
        {
            AnsiConsole.MarkupLine($"  [grey]exists, kept[/] {Markup.Escape(targetPath)}");
            return false;
        }

        var templatePath = Path.Combine("tools", "Templates", templateName.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template not found: {templatePath}");
        }

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

    /// <summary>Rebuilds the all-projects solution so new projects are in it without a second command.</summary>
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

public static class Dotnet
{
    public static async Task EnsureReference(string project, string reference)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("add");
        info.ArgumentList.Add(project);
        info.ArgumentList.Add("reference");
        info.ArgumentList.Add(reference);

        using var process = Process.Start(info)!;
        await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not reference {Markup.Escape(Path.GetFileName(reference))}: {Markup.Escape(error.Trim())}[/]");
            return;
        }

        AnsiConsole.MarkupLine($"  [green]referenced[/] {Markup.Escape(Path.GetFileName(reference))} from {Markup.Escape(Path.GetFileName(project))}");
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
