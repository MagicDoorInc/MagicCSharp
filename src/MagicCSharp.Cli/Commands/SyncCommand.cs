using MagicCSharp.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Rebuilds {Prefix}.All.slnx from what is on disk.
///     <para>
///         The create commands run this themselves, so it is rarely called directly. The two times it earns
///         its keep: after a merge leaves the solution file conflicted — regenerating beats resolving — and
///         after moving or deleting a project outside these commands.
///     </para>
/// </summary>
public class SyncCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var config = RepoConfig.Load();

        if (config == null)
        {
            return 1;
        }

        return Run(config) ? 0 : 0;
    }

    /// <summary>Returns whether the solution changed.</summary>
    public static bool Run(RepoConfig config)
    {
        var solutionFile = $"{config.Prefix}.All.slnx";
        var projects = SolutionFile.DiscoverProjects(Directory.GetCurrentDirectory());

        if (projects.Count == 0)
        {
            Output.Hint($"No projects found — leaving {solutionFile} alone.");
            return false;
        }

        if (SolutionFile.WriteGrouped(solutionFile, projects))
        {
            Output.Note($"rebuilt {solutionFile} with {projects.Count} projects");
            return true;
        }

        Output.Note($"{solutionFile} is up to date ({projects.Count} projects)");
        return false;
    }
}
