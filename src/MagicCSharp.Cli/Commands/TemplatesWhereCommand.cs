using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>Where templates are looked up.</summary>
public class TemplatesWhereCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var resolver = TemplateResolver.ForRepository(RepositoryConfig.TryLoad());

        AnsiConsole.MarkupLine("[bold]Looked up in order, first match wins:[/]");

        if (resolver.OverrideDirectory == null)
        {
            AnsiConsole.MarkupLine("  1. [grey]overrides are turned off[/] (\"templates\" is empty in magiccsharp.json)");
        }
        else
        {
            var isExisting = Directory.Exists(resolver.OverrideDirectory);
            AnsiConsole.MarkupLine(
                $"  1. {Markup.Escape(resolver.OverrideDirectory)}  {(isExisting ? "[green]exists[/]" : "[grey]absent[/]")}");
        }

        AnsiConsole.MarkupLine($"  2. built-in  [grey]({TemplateResolver.BuiltInNames().Count} templates, embedded in mcs)[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Point the first one elsewhere with \"templates\" in magiccsharp.json,[/]");
        AnsiConsole.MarkupLine("[grey]or set it to \"\" to turn overrides off.[/]");

        return 0;
    }
}
