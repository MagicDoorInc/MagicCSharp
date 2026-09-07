using System.ComponentModel;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Creates a domain inside a service.
///     <para>
///         A domain is up to three projects. <c>Default</c> holds the use cases and event handlers.
///         <c>Models</c> holds the entities, edits and filters — separate so the data projects can reference
///         the entities without reaching the logic, which is what stops a repository calling a use case.
///         <c>Tests</c> holds the tests.
///     </para>
/// </summary>
public class CreateDomainCommand : Command<CreateDomainCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-s|--solution <SOLUTION>")]
        [Description("The service's solution file, e.g. 'Acme.Shop.slnx'")]
        public string? Solution { get; init; }

        [CommandOption("-n|--name <NAME>")]
        [Description("Library name, e.g. 'Domains.Orders'")]
        public string? Name { get; init; }

        [CommandOption("-m|--models")]
        [Description("Also create a Models project. add-entity requires one.")]
        [DefaultValue(false)]
        public bool IncludeModels { get; init; }

        [CommandOption("-t|--tests")]
        [Description("Also create a Tests project")]
        [DefaultValue(false)]
        public bool IncludeTests { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Solution))
            {
                return ValidationResult.Error("Solution file is required. Use --solution <FILE>");
            }

            if (!File.Exists(Solution) || !Solution.EndsWith(".slnx", StringComparison.Ordinal))
            {
                return ValidationResult.Error($"Solution file not found or not a .slnx: {Solution}");
            }

            return Naming.IsDottedPascal(Name)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Name must be PascalCase segments separated by dots: {Name}");
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = RepoConfig.Load();

        if (config == null)
        {
            return 1;
        }

        var solution = settings.Solution!;
        var name = settings.Name!;
        var appName = config.AppNameFromSolution(solution);
        var appRoot = $"Apps/{appName}";

        if (!Directory.Exists(appRoot))
        {
            Output.Error($"Service not found: {appRoot}");
            Output.Hint($"Create it first: mcs create-app --name {appName} --database {appName.ToLowerInvariant()}");
            return 1;
        }

        // Domains.Orders -> Domains/Orders; Domains.Orders.App -> Domains/Orders/App
        var directory = $"{appRoot}/{appName}.Domains/{string.Join('/', name.Split('.').Skip(1))}";
        var assemblyName = $"{config.Prefix}.{appName}.{name}";

        var renderer = new TemplateRenderer(TemplateResolver.ForRepository(config));
        var model = new { prefix = config.Prefix, name, assembly_name = assemblyName };

        Output.Plain($"Service: {appName}   Library: {assemblyName}");
        Output.Blank();

        var projects = new List<string>();

        var defaultProject = $"{directory}/Default/{assemblyName}.csproj";
        renderer.Render("Libraries/default.csproj.hbs", defaultProject, model);
        projects.Add(defaultProject);

        if (settings.IncludeModels)
        {
            var modelsProject = $"{directory}/Models/{assemblyName}.Models.csproj";
            renderer.Render("Libraries/models.csproj.hbs", modelsProject, model);
            projects.Add(modelsProject);

            // The use cases work with the entities, so Default depends on Models. The reverse must never
            // happen: Models is what the data projects reference, and a cycle would follow.
            DotnetCli.EnsureReference(defaultProject, modelsProject);
        }

        if (settings.IncludeTests)
        {
            var testsProject = $"{directory}/Tests/{assemblyName}.Tests.csproj";
            renderer.Render("Libraries/tests.csproj.hbs", testsProject, model);
            projects.Add(testsProject);
        }

        if (SolutionFile.AddProjects(solution, projects))
        {
            Output.Updated(solution, "projects");
        }

        SyncCommand.Run(config);

        Output.Blank();
        Output.Success("Done.");

        if (settings.IncludeModels)
        {
            Output.Plain($"  mcs add-entity --solution {solution} --domain {name.Split('.').Last()} --name YourEntity --paginated");
        }
        else
        {
            Output.Note("Pass --models if you want add-entity to be able to place entities here.");
        }

        return 0;
    }
}
