#!/usr/bin/env dotnet run
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:package Spectre.Console.Cli@0.50.0
#:package Scriban@7.2.5

// Creates a service: its host project, its own solution, and — unless --no-database — the pair of data
// projects that keep repository contracts separate from their Entity Framework implementation.
//
// The service gets its own .slnx as well as an entry in the all-projects solution. That is the point of the
// layout: day to day you open one service and build one service, and the wide solution exists for the times
// you need to see everything at once.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scriban;
using Scriban.Runtime;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<CreateAppCommand>();
app.Configure(config =>
{
    config.SetApplicationName("dotnet run tools/CreateApp.cs --");
    config.AddExample("--name", "Shop", "--database", "shop");
    config.AddExample("--name", "Notifications", "--no-database");
});
return app.Run(args);

public class CreateAppSettings : CommandSettings
{
    [CommandOption("-n|--name <NAME>")]
    [Description("Service name, one PascalCase word, e.g. 'Shop'")]
    public string? Name { get; set; }

    [CommandOption("-d|--database <DATABASE>")]
    [Description("Database name, lowercase with underscores. Implies data projects.")]
    public string? Database { get; set; }

    [CommandOption("--no-database")]
    [Description("Create no data projects — for a service that owns no tables")]
    [DefaultValue(false)]
    public bool NoDatabase { get; set; }

    [CommandOption("-p|--port <PORT>")]
    [Description("Local development port. Defaults to one not already taken by another service.")]
    public int? Port { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return ValidationResult.Error("Service name is required. Use --name <NAME>");
        }

        if (!Regex.IsMatch(Name, "^[A-Z][0-9a-zA-Z]*$"))
        {
            return ValidationResult.Error($"Service name must be one PascalCase word: {Name}");
        }

        if (!string.IsNullOrWhiteSpace(Database) && !Regex.IsMatch(Database, "^[a-z][a-z0-9_]*$"))
        {
            return ValidationResult.Error($"Database name must be lowercase with underscores: {Database}");
        }

        if (!string.IsNullOrWhiteSpace(Database) && NoDatabase)
        {
            return ValidationResult.Error("Pass either --database or --no-database, not both.");
        }

        if (string.IsNullOrWhiteSpace(Database) && !NoDatabase)
        {
            return ValidationResult.Error("Pass --database <NAME> to give the service its own database, or --no-database.");
        }

        return ValidationResult.Success();
    }
}

public class CreateAppCommand : AsyncCommand<CreateAppSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreateAppSettings settings)
    {
        var config = RepoConfig.Load();
        if (config == null)
        {
            return 1;
        }

        var name = settings.Name!;
        var prefix = config.Prefix;
        var withDatabase = !settings.NoDatabase;
        var databaseName = settings.Database ?? name.ToLowerInvariant();
        var port = settings.Port ?? FindFreePort();

        var root = $"Apps/{name}";
        var solutionFile = $"{prefix}.{name}.slnx";

        if (Directory.Exists(root))
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(root)} already exists — only missing files will be created.[/]");
        }

        AnsiConsole.MarkupLine($"[green]Service:[/] {name}   [green]Port:[/] {port}   [green]Database:[/] {(withDatabase ? databaseName : "none")}");
        AnsiConsole.WriteLine();

        var model = new
        {
            prefix,
            name,
            port,
            database = new { enabled = withDatabase, name = databaseName },
        };

        var appProject = $"{root}/{name}.App/{prefix}.{name}.App.csproj";

        await Scaffold.Render("Apps/app.csproj.hbs", appProject, model);
        await Scaffold.Render("Apps/Program.cs.hbs", $"{root}/{name}.App/Program.cs", model);
        await Scaffold.Render("Apps/HelloController.cs.hbs", $"{root}/{name}.App/Controllers/HelloController.cs", model);
        await Scaffold.Render("Apps/appsettings.json.hbs", $"{root}/{name}.App/appsettings.json", model);
        await Scaffold.Render("Apps/appsettings.Development.json.hbs", $"{root}/{name}.App/appsettings.Development.json", model);
        await Scaffold.Render("Apps/launchSettings.json.hbs", $"{root}/{name}.App/Properties/launchSettings.json", model);

        var projects = new List<string> { appProject };

        if (withDatabase)
        {
            var dataModels = $"{root}/Data/Data.Models/{prefix}.{name}.Data.Models.csproj";
            var dataEf = $"{root}/Data/Data.EntityFramework/{prefix}.{name}.Data.EntityFramework.csproj";

            await Scaffold.Render("Apps/Data/DataModels.csproj.hbs", dataModels, model);
            await Scaffold.Render("Apps/Data/DataEntityFramework.csproj.hbs", dataEf, model);
            await Scaffold.Render("Apps/Data/MagicContext.cs.hbs", $"{root}/Data/Data.EntityFramework/Magic{name}Context.cs", model);
            await Scaffold.Render("Apps/Data/RepositoriesModule.cs.hbs", $"{root}/Data/Data.EntityFramework/{name}RepositoriesModule.cs", model);

            projects.Add(dataModels);
            projects.Add(dataEf);
        }

        await WriteServiceSolution(solutionFile, projects);
        await Scaffold.SyncSolution();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]Done.[/] Next:");
        AnsiConsole.MarkupLine(
            $"  dotnet run tools/CreateAppLib.cs -- --solution {Markup.Escape(solutionFile)} --name Domains.Orders --models --tests");
        AnsiConsole.MarkupLine(
            $"  dotnet run tools/AddEntity.cs -- --solution {Markup.Escape(solutionFile)} --domain Orders --name Order --paginated");
        AnsiConsole.MarkupLine($"  dotnet run --project {Markup.Escape(root)}/{Markup.Escape(name)}.App");

        return 0;
    }

    /// <summary>
    ///     Writes the service's own solution. Regenerated rather than merged into, so re-running is safe;
    ///     CreateAppLib rewrites it the same way when it adds a domain.
    /// </summary>
    private static async Task WriteServiceSolution(string solutionFile, IReadOnlyList<string> projects)
    {
        var existing = File.Exists(solutionFile)
            ? Regex.Matches(await File.ReadAllTextAsync(solutionFile), @"Path=""([^""]+)""").Select(match => match.Groups[1].Value)
            : [];

        var all = existing.Concat(projects).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        var content = "<Solution>\n" + string.Join("", all.Select(path => $"  <Project Path=\"{path}\" />\n")) + "</Solution>\n";

        if (File.Exists(solutionFile) && await File.ReadAllTextAsync(solutionFile) == content)
        {
            AnsiConsole.MarkupLine($"  [grey]up to date[/] {Markup.Escape(solutionFile)}");
            return;
        }

        await File.WriteAllTextAsync(solutionFile, content);
        AnsiConsole.MarkupLine($"  [green]written[/] {Markup.Escape(solutionFile)}");
    }

    /// <summary>
    ///     Picks a port no other service's launchSettings.json already claims, so two services started
    ///     together do not fight over one.
    /// </summary>
    private static int FindFreePort()
    {
        var taken = new HashSet<int>();

        if (Directory.Exists("Apps"))
        {
            foreach (var file in Directory.EnumerateFiles("Apps", "launchSettings.json", SearchOption.AllDirectories))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"localhost:(\d+)"))
                {
                    taken.Add(int.Parse(match.Groups[1].Value));
                }
            }
        }

        for (var port = 5200; port < 5800; port++)
        {
            if (taken.Add(port))
            {
                return port;
            }
        }

        return 5200;
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
