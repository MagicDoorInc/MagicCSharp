#!/usr/bin/env dotnet run
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:package Spectre.Console.Cli@0.50.0

// Shows which template a generator will actually use, and copies one into the repository so it can be
// changed.
//
// Overriding is per file: copy the one template you want different and the rest stay built-in, so a team
// can change how a DAL is shaped without inheriting responsibility for every other template forever.

using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("dotnet run tools/Templates.cs --");
    config.AddCommand<ListCommand>("list").WithDescription("Show every template and which layer provides it");
    config.AddCommand<EjectCommand>("eject").WithDescription("Copy a template into the repository so it can be edited");
    config.AddCommand<WhereCommand>("where").WithDescription("Show the layers, in the order they are searched");
});
return app.Run(args);

public class ListCommand : Command<ListCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--overridden")]
        [Description("Only the templates this repository overrides")]
        [DefaultValue(false)]
        public bool OnlyOverridden { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var templates = TemplateCatalog.All();

        if (templates.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No templates found.[/] Layers searched:");
            foreach (var root in TemplateResolver.Roots())
            {
                AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(root)}[/]");
            }

            return 1;
        }

        var table = new Table().Border(TableBorder.SimpleHeavy);
        table.AddColumn("Template");
        table.AddColumn("Provided by");

        var overriddenCount = 0;

        foreach (var (name, path) in templates)
        {
            var isOverride = TemplateCatalog.IsOverride(path);
            if (isOverride)
            {
                overriddenCount++;
            }
            else if (settings.OnlyOverridden)
            {
                continue;
            }

            table.AddRow(
                Markup.Escape(name),
                isOverride ? $"[green]this repository[/] [grey]({Markup.Escape(path)})[/]" : "[grey]built-in[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{templates.Count} templates, {overriddenCount} overridden[/]");

        return 0;
    }
}

public class EjectCommand : Command<EjectCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<TEMPLATE>")]
        [Description("Template to copy, e.g. 'Entities/dal.cs.hbs'")]
        public string Template { get; set; } = "";

        [CommandOption("-f|--force")]
        [Description("Overwrite an override that already exists")]
        [DefaultValue(false)]
        public bool Force { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var overrideRoot = TemplateResolver.OverrideDirectory();

        if (string.IsNullOrWhiteSpace(overrideRoot))
        {
            AnsiConsole.MarkupLine("[red]This repository has overrides turned off[/] (\"templates\" is empty in magiccsharp.json).");
            return 1;
        }

        string source;
        try
        {
            source = TemplateResolver.Resolve(settings.Template);
        }
        catch (FileNotFoundException)
        {
            AnsiConsole.MarkupLine($"[red]No such template:[/] {Markup.Escape(settings.Template)}");
            AnsiConsole.MarkupLine("[yellow]Run 'templates list' to see the names.[/]");
            return 1;
        }

        var target = Path.Combine(overrideRoot, settings.Template.Replace('/', Path.DirectorySeparatorChar));

        if (Path.GetFullPath(source) == Path.GetFullPath(target))
        {
            AnsiConsole.MarkupLine($"[blue]Already overridden here:[/] {Markup.Escape(target)}");
            return 0;
        }

        if (File.Exists(target) && !settings.Force)
        {
            AnsiConsole.MarkupLine($"[yellow]An override already exists:[/] {Markup.Escape(target)}");
            AnsiConsole.MarkupLine("[grey]Pass --force to replace it with the built-in version.[/]");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, settings.Force);

        AnsiConsole.MarkupLine($"[green]Copied[/] {Markup.Escape(settings.Template)}");
        AnsiConsole.MarkupLine($"  [grey]from[/] {Markup.Escape(source)}");
        AnsiConsole.MarkupLine($"  [grey]to  [/] {Markup.Escape(target)}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Commit it, and every generator in this repository uses your copy from now on.[/]");
        AnsiConsole.MarkupLine("[grey]Delete it to go back to the built-in.[/]");

        return 0;
    }
}

public class WhereCommand : Command
{
    public override int Execute(CommandContext context)
    {
        AnsiConsole.MarkupLine("[bold]Searched in order, first match wins:[/]");

        var index = 1;
        foreach (var root in TemplateResolver.Roots())
        {
            var exists = Directory.Exists(root);
            var label = exists ? "[green]exists[/]" : "[grey]absent[/]";
            AnsiConsole.MarkupLine($"  {index++}. {Markup.Escape(root)}  {label}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Point the first one somewhere else with \"templates\" in magiccsharp.json,[/]");
        AnsiConsole.MarkupLine("[grey]or set it to \"\" to turn overrides off.[/]");

        return 0;
    }
}

public static class TemplateCatalog
{
    /// <summary>
    ///     Every template reachable from any layer, keyed by its relative name, with the path that would
    ///     actually be used.
    /// </summary>
    public static IReadOnlyList<(string Name, string Path)> All()
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Roots are most-specific first, so the first sighting of a name is the one that wins.
        foreach (var root in TemplateResolver.Roots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.hbs", SearchOption.AllDirectories))
            {
                var name = Path.GetRelativePath(root, file).Replace('\\', '/');
                seen.TryAdd(name, file);
            }
        }

        return seen.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => (pair.Key, pair.Value))
            .ToList();
    }

    /// <summary>Whether a resolved path came from the repository rather than the installed templates.</summary>
    public static bool IsOverride(string resolvedPath)
    {
        var overrideRoot = TemplateResolver.OverrideDirectory();

        if (string.IsNullOrWhiteSpace(overrideRoot) || !Directory.Exists(overrideRoot))
        {
            return false;
        }

        var full = Path.GetFullPath(resolvedPath);
        var root = Path.GetFullPath(overrideRoot);

        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
