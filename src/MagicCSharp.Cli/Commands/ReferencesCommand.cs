using System.ComponentModel;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Prints every project a project depends on, directly or through another project, one folder per line.
///     <para>
///         The folders are what a CI trigger needs: a change under any of them can change what the project
///         builds into. Plain lines on standard output, so a script can read them without parsing.
///     </para>
/// </summary>
public class ReferencesCommand : Command<ReferencesCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-p|--project <CSPROJ>")]
        [Description("The project to start from.")]
        public string Project { get; init; } = "";

        [CommandOption("--files")]
        [Description("Print the .csproj paths instead of their folders.")]
        public bool ShouldPrintFiles { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Project))
            {
                return ValidationResult.Error("--project is required.");
            }

            return File.Exists(Project)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Project not found: {Project}");
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var root = Directory.GetCurrentDirectory();
        var project = Path.GetFullPath(settings.Project);

        IReadOnlyList<string> lines = settings.ShouldPrintFiles
            ? ProjectGraph.Closure(project).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Order(StringComparer.Ordinal).ToList()
            : ProjectGraph.Folders(root, project);

        foreach (var line in lines)
        {
            Console.Out.WriteLine(line);
        }

        return 0;
    }
}
