using MagicCSharp.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Refreshes <c>CLAUDE.md</c> and the <c>.ai-knowledge/</c> guides to the version this mcs ships, so a
///     repository picks up improved guidance by upgrading the tool and running one command.
///     <para>
///         It overwrites the files mcs ships and nothing else: <c>.ai-knowledge/project.md</c> and any other file
///         under <c>.ai-knowledge/</c> are the repository's. The change is an ordinary diff to review.
///     </para>
/// </summary>
public class UpdateAiFilesCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var config = RepositoryConfig.Load();

        if (config == null)
        {
            return 1;
        }

        var changedCount = AiFiles.Update("", config.Prefix);

        Output.Blank();

        if (changedCount == 0)
        {
            Output.Note($"The AI files already match mcs {ToolVersion.Current}.");
            return 0;
        }

        Output.Success($"{changedCount} file(s) brought to mcs {ToolVersion.Current}.");
        Output.Hint("Review the change with 'git diff'. What is specific to this repository belongs in .ai-knowledge/project.md, which this never overwrites.");

        return 0;
    }
}
