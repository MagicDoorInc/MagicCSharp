using System.ComponentModel;
using MagicCSharp.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace MagicCSharp.Cli.Commands;

/// <summary>
///     Turns a directory into a repository the other commands understand.
///     <para>
///         Everything it writes is ordinary, editable configuration. Nothing is overwritten, so it composes
///         with a repository that already has a README, a .gitignore and projects of its own.
///     </para>
/// </summary>
public class InitCommand : Command<InitCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-p|--prefix <PREFIX>")]
        [Description("Namespace and solution-name root, e.g. 'Acme' gives Acme.Shop.slnx")]
        public string? Prefix { get; init; }

        [CommandOption("--package-version <VERSION>")]
        [Description("MagicCSharp version to pin in Directory.Packages.props")]
        public string? PackageVersion { get; init; }

        [CommandOption("--target-framework <TFM>")]
        [Description("Target framework for the repository's projects")]
        [DefaultValue("net10.0")]
        public string TargetFramework { get; init; } = "net10.0";

        public override Spectre.Console.ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Prefix))
            {
                return Spectre.Console.ValidationResult.Error("Prefix is required. Use --prefix <PREFIX>");
            }

            return Naming.IsPascalWord(Prefix)
                ? Spectre.Console.ValidationResult.Success()
                : Spectre.Console.ValidationResult.Error($"Prefix must be one PascalCase word: {Prefix}");
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var prefix = settings.Prefix!;

        // Defaults to the version this tool shipped with, so a new repository never pins something older
        // than its own tooling.
        var version = settings.PackageVersion ?? ToolVersion.Current;

        var renderer = new TemplateRenderer(TemplateResolver.ForRepository(null));
        var model = new { prefix, version, target_framework = settings.TargetFramework };

        Output.Plain($"Prefix: {prefix}   MagicCSharp: {version}   Target: {settings.TargetFramework}");
        Output.Blank();

        var wrote = false;

        // "templates" is written even though it matches the default, so the override mechanism is
        // discoverable from the config rather than only from documentation.
        wrote |= TemplateRenderer.Write(RepoConfig.FileName, $$"""
            {
              "prefix": "{{prefix}}",
              "templates": "{{TemplateResolver.DefaultOverrideDirectory}}"
            }

            """);

        wrote |= renderer.Render("Repo/Directory.Build.props.hbs", "Directory.Build.props", model);
        wrote |= renderer.Render("Repo/Directory.Packages.props.hbs", "Directory.Packages.props", model);
        wrote |= TemplateRenderer.Write($"{prefix}.All.slnx", "<Solution>\n</Solution>\n");

        // git does not track empty directories, so without these the two top-level directories vanish on
        // a fresh clone.
        wrote |= TemplateRenderer.Write(Path.Combine("Apps", ".gitkeep"), "");
        wrote |= TemplateRenderer.Write(Path.Combine("Libs", ".gitkeep"), "");

        // Without one, the first build leaves hundreds of bin/ and obj/ files staged.
        wrote |= renderer.Render("Repo/gitignore.hbs", ".gitignore", model);

        Output.Blank();

        if (!wrote)
        {
            Output.Note("Nothing to do — this repository is already set up.");
            return 0;
        }

        Output.Success("Ready.");
        Output.Plain("  mcs create-app --name Shop --database shop");
        Output.Plain($"  mcs create-domain --solution {prefix}.Shop.slnx --name Domains.Orders --models --tests");

        return 0;
    }
}

/// <summary>The version of this tool, read from the assembly rather than hardcoded in two places.</summary>
public static class ToolVersion
{
    public static string Current { get; } =
        typeof(ToolVersion).Assembly.GetName().Version is { } version
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";
}
