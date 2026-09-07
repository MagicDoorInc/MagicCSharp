using System.ComponentModel;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Creates a domain inside a service.
///     <para>
///         A domain is up to three projects. <c>Default</c> holds the use cases and event handlers.
///         <c>Models</c> holds the entities, edits and filters — separate so the data projects can
///         reference the entities without reaching the logic, which is what stops a repository calling a
///         use case. <c>Tests</c> holds the tests.
///     </para>
///     <para>
///         Dots go deeper. <c>Orders.App</c> is the domain's HTTP surface, holding its controllers, so the
///         service's host project stays a shell rather than collecting every domain's endpoints.
///         <c>Orders.Fulfilment</c> is a subdomain, for when one domain has grown enough to split.
///     </para>
///     <para>
///         This is <c>create-app-lib</c> with <c>Domains.</c> prepended, and nothing else.
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
        [Description("Domain name, e.g. 'Orders'. 'Orders.App' is its endpoints.")]
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
            // The name is checked in AppLibrary.Plan, which needs the service to say anything useful.
            return string.IsNullOrWhiteSpace(Name)
                ? ValidationResult.Error("Domain name is required. Use --name <NAME>")
                : ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var name = settings.Name!;

        if (name == "Domains")
        {
            Output.Error("Domains is the container, not a domain. Name the domain itself:");
            Output.Hint("  mcs create-domain --name Orders");
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

        return CreateAppLibCommand.Run(settings.Solution, name, settings.IncludeModels, settings.IncludeTests);
    }
}
