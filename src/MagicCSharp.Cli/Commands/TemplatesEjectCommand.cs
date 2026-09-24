using System.ComponentModel;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

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
        public bool ShouldOverwrite { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var resolver = TemplateResolver.ForRepository(RepositoryConfig.TryLoad());

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

        if (File.Exists(target) && !settings.ShouldOverwrite)
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
