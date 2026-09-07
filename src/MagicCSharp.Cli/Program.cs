using MagicCSharp.Cli.Commands;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("mcs");
    config.SetApplicationVersion(ToolVersion.Current);

    config.AddCommand<InitCommand>("init")
        .WithDescription("Set the current directory up as a MagicCSharp repository")
        .WithExample("init", "--prefix", "Acme");

    config.AddCommand<CreateAppCommand>("create-app")
        .WithDescription("Create a service")
        .WithExample("create-app", "--name", "Shop", "--database", "shop");

    config.AddCommand<CreateDomainCommand>("create-domain")
        .WithDescription("Create a domain inside a service")
        .WithExample("create-domain", "-s", "Shop", "-n", "Orders", "--models", "--tests");

    config.AddCommand<CreateAppLibCommand>("create-app-lib")
        .WithDescription("Create a library inside one service")
        .WithExample("create-app-lib", "-s", "Shop", "-n", "Processors", "--tests");

    config.AddCommand<CreateLibCommand>("create-lib")
        .WithDescription("Create a shared library under Libs/")
        .WithExample("create-lib", "--name", "Events", "--tests");

    config.AddCommand<AddEntityCommand>("add-entity")
        .WithDescription("Create an entity and its repository")
        .WithExample("add-entity", "-s", "Shop", "-d", "Orders", "-n", "Order", "--paginated");

    config.AddCommand<SyncCommand>("sync")
        .WithDescription("Rebuild the all-projects solution from disk");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Lint the conventions the compiler cannot");

    config.AddBranch("templates", templates =>
    {
        templates.SetDescription("See and override the templates the generators use");
        templates.AddCommand<TemplatesListCommand>("list").WithDescription("Every template, and which layer provides it");
        templates.AddCommand<TemplatesWhereCommand>("where").WithDescription("Where templates are looked up");
        templates.AddCommand<TemplatesEjectCommand>("eject").WithDescription("Copy a template into this repository to customise");
    });

    // A wrong flag or a missing template is the user's problem to fix, and a stack trace helps nobody
    // read it. Only a genuinely unexpected exception gets the full dump, because that one is ours.
    config.SetExceptionHandler((exception, _) =>
    {
        switch (exception)
        {
            case CommandParseException parse:
                Output.Error(parse.Message);
                Output.Hint("Run 'mcs --help', or '<command> --help', for the options.");
                return 1;
            case CommandRuntimeException runtime:
                Output.Error(runtime.Message);
                return 1;
            case TemplateNotFoundException:
            case InvalidOperationException:
            case IOException:
            case UnauthorizedAccessException:
                Output.Error(exception.Message);
                return 1;
            default:
                AnsiConsole.WriteException(exception, ExceptionFormats.ShortenPaths);
                return 1;
        }
    });
});

return app.Run(args);
