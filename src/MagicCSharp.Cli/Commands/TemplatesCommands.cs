using System.ComponentModel;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>Every template, and which layer provides it.</summary>
public class TemplatesListCommand : Command<TemplatesListCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--overridden")]
        [Description("Only the templates this repository overrides")]
        [DefaultValue(false)]
        public bool OnlyOverridden { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var resolver = TemplateResolver.ForRepository(RepoConfig.TryLoad());
        var templates = resolver.All();

        var table = new Table().Border(TableBorder.SimpleHeavy);
        table.AddColumn("Template");
        table.AddColumn("Provided by");

        var overridden = 0;

        foreach (var template in templates)
        {
            if (template.IsOverride)
            {
                overridden++;
            }
            else if (settings.OnlyOverridden)
            {
                continue;
            }

            table.AddRow(
                Markup.Escape(template.Name),
                template.IsOverride
                    ? $"[green]this repository[/] [grey]({Markup.Escape(template.Location)})[/]"
                    : "[grey]built-in[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{templates.Count} templates, {overridden} overridden[/]");

        return 0;
    }
}

/// <summary>Where templates are looked up.</summary>
public class TemplatesWhereCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var resolver = TemplateResolver.ForRepository(RepoConfig.TryLoad());

        AnsiConsole.MarkupLine("[bold]Looked up in order, first match wins:[/]");

        if (resolver.OverrideDirectory == null)
        {
            AnsiConsole.MarkupLine("  1. [grey]overrides are turned off[/] (\"templates\" is empty in magiccsharp.json)");
        }
        else
        {
            var exists = Directory.Exists(resolver.OverrideDirectory);
            AnsiConsole.MarkupLine(
                $"  1. {Markup.Escape(resolver.OverrideDirectory)}  {(exists ? "[green]exists[/]" : "[grey]absent[/]")}");
        }

        AnsiConsole.MarkupLine($"  2. built-in  [grey]({TemplateResolver.BuiltInNames().Count} templates, embedded in mcs)[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Point the first one elsewhere with \"templates\" in magiccsharp.json,[/]");
        AnsiConsole.MarkupLine("[grey]or set it to \"\" to turn overrides off.[/]");

        return 0;
    }
}

/// <summary>Copies a built-in template into the repository so it can be changed.</summary>
public class TemplatesEjectCommand : Command<TemplatesEjectCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<TEMPLATE>")]
        [Description("Template to copy, e.g. 'Entities/dal.cs.hbs'")]
        public string Template { get; init; } = "";

        [CommandOption("-f|--force")]
        [Description("Replace an override that already exists")]
        [DefaultValue(false)]
        public bool Force { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var resolver = TemplateResolver.ForRepository(RepoConfig.TryLoad());

        if (resolver.OverrideDirectory == null)
        {
            Output.Error("This repository has overrides turned off (\"templates\" is empty in magiccsharp.json).");
            return 1;
        }

        var builtIn = TemplateResolver.ReadBuiltIn(settings.Template);

        if (builtIn == null)
        {
            Output.Error($"No such template: {settings.Template}");
            Output.Hint("Run 'mcs templates list' to see the names.");
            return 1;
        }

        var target = resolver.OverridePath(settings.Template)!;

        if (File.Exists(target) && !settings.Force)
        {
            Output.Hint($"An override already exists: {target}");
            Output.Note("Pass --force to replace it with the current built-in.");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, builtIn);

        Output.Created(target);
        Output.Blank();

        // An override only reaches teammates if it is committed, and this is the moment to say so — the
        // file exists, it works locally, and nothing yet suggests it is not shared.
        if (Git.IsIgnored(target))
        {
            Output.Hint("This path is git-ignored, so the override will not reach anyone else.");
            Output.Note($"Un-ignore {resolver.OverrideDirectory} before committing.");
        }
        else
        {
            Output.Note("Commit it so your team uses the same generated code:");
            Output.Plain($"  git add {target} && git commit -m \"Use our own {settings.Template}\"");
        }

        Output.Blank();
        Output.Note("Every generator in this repository uses your copy from now on. Delete it to go back.");

        return 0;
    }
}
