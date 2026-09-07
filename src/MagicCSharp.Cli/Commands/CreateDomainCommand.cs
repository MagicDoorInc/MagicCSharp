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
        [CommandOption("-s|--solution <SERVICE>")]
        [Description("Which service, e.g. 'Shop'. Omit when the repository has only one.")]
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
            // The solution is not checked here: resolving a service name, or falling back to the only
            // service there is, needs the repository config, which is not loaded until Execute.
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

        var solution = SolutionArgument.Resolve(config, settings.Solution);

        if (solution == null)
        {
            return 1;
        }

        var name = settings.Name!;
        var appName = config.AppNameFromSolution(solution);
        var appRoot = $"Apps/{appName}";

        if (!Directory.Exists(appRoot))
        {
            Output.Error($"Service not found: {appRoot}");
            Output.Hint($"Create it first: mcs create-app --name {appName} --database {appName.ToLowerInvariant()}");
            return 1;
        }

        // "Orders" is what people type; "Domains.Orders" is what the layout wants. Accepting the short
        // form and silently building Shop.Domains//Default from it — a double slash and a wrong assembly
        // name — was worse than either accepting or rejecting it outright.
        if (!name.StartsWith("Domains.", StringComparison.Ordinal))
        {
            name = $"Domains.{name}";
            Output.Note($"Interpreting --name as {name}");
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

        // The service has to reference the domain, or nothing happens: the assembly is not deployed with
        // the app, so its use cases are never registered and its event handlers never run. Scaffolding a
        // domain and leaving it unreferenced looked like it worked and did nothing.
        var appProject = $"{appRoot}/{appName}.App/{config.Prefix}.{appName}.App.csproj";

        if (File.Exists(appProject))
        {
            DotnetCli.EnsureReference(appProject, defaultProject);
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
            Output.Plain($"  mcs add-entity --solution {appName} --domain {name.Split('.').Last()} --name YourEntity --paginated");
        }
        else
        {
            Output.Note("Pass --models if you want add-entity to be able to place entities here.");
        }

        return 0;
    }
}
