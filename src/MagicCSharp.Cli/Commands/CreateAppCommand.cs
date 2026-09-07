using System.ComponentModel;
using System.Text.RegularExpressions;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Creates a service: its host project, its own solution, and — unless <c>--no-database</c> — the pair of
///     data projects that keep repository contracts separate from their Entity Framework implementation.
///     <para>
///         The service gets its own .slnx as well as an entry in the all-projects solution. That is the point
///         of the layout: day to day you open one service and build one service, and the wide solution exists
///         for the times you need to see everything.
///     </para>
/// </summary>
public partial class CreateAppCommand : Command<CreateAppCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-n|--name <NAME>")]
        [Description("Service name, one PascalCase word, e.g. 'Shop'")]
        public string? Name { get; init; }

        [CommandOption("-d|--database <DATABASE>")]
        [Description("Database name, lowercase with underscores. Implies data projects.")]
        public string? Database { get; init; }

        [CommandOption("--no-database")]
        [Description("Create no data projects — for a service that owns no tables")]
        [DefaultValue(false)]
        public bool NoDatabase { get; init; }

        [CommandOption("-p|--port <PORT>")]
        [Description("Local development port. Defaults to one no other service has claimed.")]
        public int? Port { get; init; }

        public override ValidationResult Validate()
        {
            if (!Naming.IsPascalWord(Name))
            {
                return ValidationResult.Error($"Service name must be one PascalCase word: {Name}");
            }

            if (Database != null && !Naming.IsDatabaseName(Database))
            {
                return ValidationResult.Error($"Database name must be lowercase with underscores: {Database}");
            }

            if (Database != null && NoDatabase)
            {
                return ValidationResult.Error("Pass either --database or --no-database, not both.");
            }

            if (Database == null && !NoDatabase)
            {
                return ValidationResult.Error("Pass --database <NAME> to give the service a database, or --no-database.");
            }

            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = RepoConfig.Load();

        if (config == null)
        {
            return 1;
        }

        var name = settings.Name!;
        var prefix = config.Prefix;
        var withDatabase = !settings.NoDatabase;
        var databaseName = settings.Database ?? name.ToLowerInvariant();
        var port = settings.Port ?? FindFreePort();

        var root = $"Apps/{name}";
        var solutionFile = $"{prefix}.{name}.slnx";

        Output.Plain($"Service: {name}   Port: {port}   Database: {(withDatabase ? databaseName : "none")}");
        Output.Blank();

        var renderer = new TemplateRenderer(TemplateResolver.ForRepository(config));
        var model = new
        {
            prefix,
            name,
            port,
            database = new { enabled = withDatabase, name = databaseName },
        };

        var appProject = $"{root}/{name}.App/{prefix}.{name}.App.csproj";
        var projects = new List<string> { appProject };

        renderer.Render("Apps/app.csproj.hbs", appProject, model);
        renderer.Render("Apps/Program.cs.hbs", $"{root}/{name}.App/Program.cs", model);
        renderer.Render("Apps/HelloController.cs.hbs", $"{root}/{name}.App/Controllers/HelloController.cs", model);
        renderer.Render("Apps/appsettings.json.hbs", $"{root}/{name}.App/appsettings.json", model);
        renderer.Render("Apps/appsettings.Development.json.hbs", $"{root}/{name}.App/appsettings.Development.json", model);
        renderer.Render("Apps/launchSettings.json.hbs", $"{root}/{name}.App/Properties/launchSettings.json", model);

        if (withDatabase)
        {
            var dataModels = $"{root}/Data/Data.Models/{prefix}.{name}.Data.Models.csproj";
            var dataEf = $"{root}/Data/Data.EntityFramework/{prefix}.{name}.Data.EntityFramework.csproj";

            renderer.Render("Apps/Data/DataModels.csproj.hbs", dataModels, model);
            renderer.Render("Apps/Data/DataEntityFramework.csproj.hbs", dataEf, model);
            renderer.Render("Apps/Data/MagicContext.cs.hbs", $"{root}/Data/Data.EntityFramework/Magic{name}Context.cs", model);
            renderer.Render("Apps/Data/RepositoriesModule.cs.hbs", $"{root}/Data/Data.EntityFramework/{name}RepositoriesModule.cs", model);

            projects.Add(dataModels);
            projects.Add(dataEf);
        }

        if (SolutionFile.AddProjects(solutionFile, projects))
        {
            Output.Updated(solutionFile, "projects");
        }
        else
        {
            Output.Note($"{solutionFile} is up to date");
        }

        SyncCommand.Run(config);

        Output.Blank();
        Output.Success("Done.");
        Output.Plain($"  mcs create-domain --solution {solutionFile} --name Domains.Orders --models --tests");
        Output.Plain($"  dotnet run --project {root}/{name}.App");

        return 0;
    }

    /// <summary>
    ///     A port no other service's launchSettings.json already claims, so two services started together do
    ///     not fight over one.
    /// </summary>
    private static int FindFreePort()
    {
        var taken = new HashSet<int>();

        if (Directory.Exists("Apps"))
        {
            foreach (var file in Directory.EnumerateFiles("Apps", "launchSettings.json", SearchOption.AllDirectories))
            {
                foreach (Match match in LocalhostPort().Matches(File.ReadAllText(file)))
                {
                    taken.Add(int.Parse(match.Groups[1].Value));
                }
            }
        }

        for (var port = 5200; port < 5800; port++)
        {
            if (taken.Add(port))
            {
                return port;
            }
        }

        return 5200;
    }

    [GeneratedRegex("localhost:([0-9]+)")]
    private static partial Regex LocalhostPort();
}
