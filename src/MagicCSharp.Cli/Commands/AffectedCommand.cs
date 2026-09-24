using System.ComponentModel;
using System.Text.Json;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Prints the apps a set of commits changed, so CI builds and deploys only those.
///     <para>
///         An app is affected when a changed file sits in its own folder or in a project it depends on, or when
///         the change is to a file every build reads (Directory.Build.props, Directory.Packages.props,
///         global.json …). By default "depends on" means what the host project reaches — what ships, so what to
///         deploy. <c>--solution</c> widens it to every project in the app's solution, tests included — what to
///         test.
///     </para>
/// </summary>
public class AffectedCommand : Command<AffectedCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-b|--base <COMMIT>")]
        [Description("The commit to compare against: the pull request's base, or the last deployed commit.")]
        public string? Base { get; init; }

        [CommandOption("--head <COMMIT>")]
        [Description("The commit to compare. Defaults to HEAD.")]
        [DefaultValue("HEAD")]
        public string Head { get; init; } = "HEAD";

        [CommandOption("--solution")]
        [Description("Count every project in the app's solution, tests included, not only what the app ships.")]
        public bool ShouldUseSolution { get; init; }

        [CommandOption("--all")]
        [Description("Print every app, ignoring what changed. For a manual full deploy.")]
        public bool ShouldIncludeAll { get; init; }

        [CommandOption("--json")]
        [Description("Print a JSON array of names, ready for a GitHub Actions matrix.")]
        public bool ShouldPrintJson { get; init; }

        public override ValidationResult Validate()
        {
            return ShouldIncludeAll || !string.IsNullOrWhiteSpace(Base)
                ? ValidationResult.Success()
                : ValidationResult.Error("--base is required unless --all is set.");
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = RepositoryConfig.Load();

        if (config == null)
        {
            return 1;
        }

        var apps = AppGraphs.Load(Directory.GetCurrentDirectory(), config.Prefix, settings.ShouldUseSolution);
        var names = settings.ShouldIncludeAll
            ? apps.Select(app => app.Name).ToList()
            : AffectedApps.Find(apps, Git.ChangedPaths(settings.Base!, settings.Head));

        if (settings.ShouldPrintJson)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(names));
            return 0;
        }

        foreach (var name in names)
        {
            Console.Out.WriteLine(name);
        }

        return 0;
    }
}
