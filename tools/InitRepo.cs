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
    /// <summary>
    ///     Where the .hbs templates live.
    ///     <para>
    ///         When the tools are installed globally the scripts run from outside the repository, so
    ///         <c>tools/Templates</c> relative to the working directory is wrong — the installer sets
    ///         MAGICCSHARP_TOOLS_DIR to the install location. Falling back to the relative path keeps a
    ///         repository that vendored <c>tools/</c> working unchanged.
    ///     </para>
    /// </summary>
    private static string TemplateRoot
    {
        get
        {
            var installed = Environment.GetEnvironmentVariable("MAGICCSHARP_TOOLS_DIR");
            return string.IsNullOrWhiteSpace(installed)
                ? Path.Combine("tools", "Templates")
                : Path.Combine(installed, "Templates");
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, InitRepoSettings settings)
    {
        var prefix = settings.Prefix!;
        var model = new { prefix, version = settings.PackageVersion };

        AnsiConsole.MarkupLine($"[green]Prefix:[/] {prefix}   [green]MagicCSharp:[/] {settings.PackageVersion}");
        AnsiConsole.WriteLine();

        var wrote = false;

        wrote |= await WriteIfMissing("magiccsharp.json", $$"""
                                                           {
                                                             "prefix": "{{prefix}}"
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

        var templatePath = Path.Combine(TemplateRoot, templateName.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(templatePath))
        {
            AnsiConsole.MarkupLine($"[red]Template not found:[/] {Markup.Escape(templatePath)}");
            AnsiConsole.MarkupLine("[yellow]Run this from the repository root, with tools/ alongside.[/]");
            throw new FileNotFoundException(templatePath);
        }

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
