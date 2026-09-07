using System.ComponentModel;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Creates a library inside one service.
///     <para>
///         Dots nest directories: <c>Processors</c> is <c>Apps/Shop/Shop.Processors</c>,
///         <c>Clients.Stripe</c> is <c>Apps/Shop/Shop.Clients/Stripe</c>. What the library is for follows
///         from the name — see <see cref="AppLibrary" /> — rather than from a flag.
///     </para>
///     <para>
///         <c>create-domain</c> is this command with <c>Domains.</c> prepended.
///     </para>
/// </summary>
public class CreateAppLibCommand : Command<CreateAppLibCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-s|--solution <SERVICE>")]
        [Description("Which service, e.g. 'Shop'. Omit when the repository has only one.")]
        public string? Solution { get; init; }

        [CommandOption("-n|--name <NAME>")]
        [Description("Library name, e.g. 'Processors' or 'Clients.Stripe'. Dots nest directories.")]
        public string? Name { get; init; }

        [CommandOption("-m|--models")]
        [Description("Also create a Models project")]
        [DefaultValue(false)]
        public bool IncludeModels { get; init; }

        [CommandOption("-t|--tests")]
        [Description("Also create a Tests project")]
        [DefaultValue(false)]
        public bool IncludeTests { get; init; }

        public override ValidationResult Validate()
        {
            // The name is checked in AppLibrary.Plan, which needs the service to say anything useful.
            return string.IsNullOrWhiteSpace(Name)
                ? ValidationResult.Error("Library name is required. Use --name <NAME>")
                : ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        return Run(settings.Solution, settings.Name!, settings.IncludeModels, settings.IncludeTests);
    }

    /// <summary>
    ///     Scaffolds the library. Shared with <see cref="CreateDomainCommand" />, which differs only in
    ///     prepending <c>Domains.</c> to the name.
    /// </summary>
    public static int Run(string? solutionArgument, string name, bool includeModels, bool includeTests)
    {
        var config = RepoConfig.Load();

        if (config == null)
        {
            return 1;
        }

        var solution = SolutionArgument.Resolve(config, solutionArgument);

        if (solution == null)
        {
            return 1;
        }

        var appName = config.AppNameFromSolution(solution);

        if (!Directory.Exists($"Apps/{appName}"))
        {
            Output.Error($"Service not found: Apps/{appName}");
            Output.Hint($"Create it first: mcs create-app --name {appName} --database {appName.ToLowerInvariant()}");
            return 1;
        }

        var library = AppLibrary.Plan(config, appName, name);

        if (library == null)
        {
            return 1;
        }

        var renderer = new TemplateRenderer(TemplateResolver.ForRepository(config));
        var model = new
        {
            prefix = config.Prefix,
            name = library.Name,
            app_name = appName,
            assembly_name = library.AssemblyName,
        };

        Output.Plain($"Service: {appName}   Library: {library.AssemblyName}");
        Output.Blank();

        var projects = new List<string> { library.DefaultProject };
        renderer.Render(library.DefaultTemplate, library.DefaultProject, model);

        if (includeModels)
        {
            renderer.Render("Libraries/models.csproj.hbs", library.ModelsProject, model);
            projects.Add(library.ModelsProject);

            // The code works with the types, so Default depends on Models. Never the reverse: Models is
            // what the data projects reference, and a cycle would follow.
            DotnetCli.EnsureReference(library.DefaultProject, library.ModelsProject);
        }

        if (includeTests)
        {
            renderer.Render("Libraries/tests.csproj.hbs", library.TestsProject, model);
            projects.Add(library.TestsProject);
        }

        WireParent(library);
        WireHost(library);

        if (SolutionFile.AddProjects(solution, projects))
        {
            Output.Updated(solution, "projects");
        }

        SyncCommand.Run(config);

        Output.Blank();
        Output.Success("Done.");
        NextSteps(library, includeModels);

        return 0;
    }

    /// <summary>
    ///     An HTTP surface exists to expose the library it sits inside, so it is wired to it. Any other
    ///     nested library is only usually a dependant of its parent, and a project reference nothing needs
    ///     never announces itself — so that one is offered rather than added.
    /// </summary>
    private static void WireParent(AppLibrary library)
    {
        if (library.ParentDefaultProject == null)
        {
            return;
        }

        if (!File.Exists(library.ParentDefaultProject))
        {
            if (library.IsHttpSurface)
            {
                Output.Note($"{Path.GetFileNameWithoutExtension(library.ParentDefaultProject)} does not exist yet.");
                Output.Hint("Create it, then run this again — the reference is added on the second run.");
            }

            return;
        }

        if (library.IsHttpSurface)
        {
            DotnetCli.EnsureReference(library.DefaultProject, library.ParentDefaultProject);
        }
    }

    /// <summary>
    ///     The service has to reference a domain or nothing happens: the assembly is not deployed with the
    ///     app, so its use cases are never registered and its event handlers never run.
    /// </summary>
    private static void WireHost(AppLibrary library)
    {
        if (library.IsDomain && File.Exists(library.HostProject))
        {
            DotnetCli.EnsureReference(library.HostProject, library.DefaultProject);
        }
    }

    private static void NextSteps(AppLibrary library, bool includeModels)
    {
        if (library.IsHttpSurface)
        {
            Output.Note("Controllers here are served by the host with no further wiring — it references this project.");
            return;
        }

        if (!library.IsDomain)
        {
            // Deliberately not wired to the host: an app library supports the domains, and something in a
            // domain should be what depends on it.
            Output.Note("Nothing references this yet. Use it from a domain:");
            Output.Plain($"  dotnet add <project> reference {library.DefaultProject}");
            return;
        }

        if (library.ParentDefaultProject != null && File.Exists(library.ParentDefaultProject))
        {
            Output.Note("A subdomain may depend on its parent, never the reverse. If it should:");
            Output.Plain($"  dotnet add {library.DefaultProject} reference {library.ParentDefaultProject}");
        }

        if (includeModels)
        {
            Output.Plain($"  mcs add-entity --solution {library.AppName} --domain {library.EntityDomain} --name YourEntity --paginated");
        }
        else
        {
            Output.Note("Pass --models if you want add-entity to be able to place entities here.");
        }
    }
}
