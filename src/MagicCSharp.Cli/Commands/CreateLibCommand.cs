using System.ComponentModel;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Creates a shared library under <c>Libs/</c> — code used by more than one service.
///     <para>
///         A dotted name nests: <c>Clients.Billing</c> lands in <c>Libs/Clients/Billing/</c>, which keeps a
///         dozen client libraries from sitting flat next to a dozen unrelated ones.
///     </para>
/// </summary>
public class CreateLibCommand : Command<CreateLibCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-n|--name <NAME>")]
        [Description("Library name; dots nest directories, e.g. 'Clients.Billing'")]
        public string? Name { get; init; }

        [CommandOption("-m|--models")]
        [Description("Also create a Models project")]
        [DefaultValue(false)]
        public bool ShouldIncludeModels { get; init; }

        [CommandOption("-t|--tests")]
        [Description("Also create a Tests project alongside it")]
        [DefaultValue(false)]
        public bool ShouldIncludeTests { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return ValidationResult.Error("Library name is required. Use --name <NAME>");
            }

            return Naming.IsDottedPascal(Name)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Name must be PascalCase segments separated by dots: {Name}");
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = RepositoryConfig.Load();

        if (config == null)
        {
            return 1;
        }

        var name = settings.Name!;
        var directory = $"Libs/{name.Replace('.', '/')}";
        var assemblyName = $"{config.Prefix}.Libraries.{name}";

        var templateRenderer = new TemplateRenderer(TemplateResolver.ForRepository(config));
        var model = new { prefix = config.Prefix, name, assembly_name = assemblyName };

        Output.Plain($"Library: {assemblyName}");
        Output.Blank();

        var defaultProject = $"{directory}/Default/{assemblyName}.csproj";
        var isCreated = templateRenderer.Render("Libraries/default.csproj.hbs", defaultProject, model);

        if (settings.ShouldIncludeModels)
        {
            var modelsProject = $"{directory}/Models/{assemblyName}.Models.csproj";
            isCreated |= templateRenderer.Render("Libraries/models.csproj.hbs", modelsProject, model);

            // Default depends on Models, never the reverse — Models is the half other projects reference.
            DotnetCli.EnsureReference(defaultProject, modelsProject);
        }

        if (settings.ShouldIncludeTests)
        {
            isCreated |= templateRenderer.Render("Libraries/tests.csproj.hbs", $"{directory}/Tests/{assemblyName}.Tests.csproj", model);
        }

        if (isCreated)
        {
            SyncCommand.Run(config);
        }

        Output.Blank();

        if (!isCreated)
        {
            Output.Note("Nothing to do — everything requested already exists.");
            return 0;
        }

        Output.Success("Done.");
        Output.Plain($"  dotnet add <project> reference {directory}/Default/{assemblyName}.csproj");

        return 0;
    }
}
