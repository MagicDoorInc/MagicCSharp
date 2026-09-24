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
        public bool ShouldShowOnlyOverridden { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var resolver = TemplateResolver.ForRepository(RepositoryConfig.TryLoad());
        var templates = resolver.All();

        var table = new Table().Border(TableBorder.SimpleHeavy);
        table.AddColumn("Template");
        table.AddColumn("Provided by");

        var overridden = 0;

        foreach (var templateSource in templates)
        {
            if (templateSource.IsOverride)
            {
                overridden++;
            }
            else if (settings.ShouldShowOnlyOverridden)
            {
                continue;
            }

            table.AddRow(
                Markup.Escape(templateSource.Name),
                templateSource.IsOverride
                    ? $"[green]this repository[/] [grey]({Markup.Escape(templateSource.Location)})[/]"
                    : "[grey]built-in[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{templates.Count} templates, {overridden} overridden[/]");

        return 0;
    }
}
