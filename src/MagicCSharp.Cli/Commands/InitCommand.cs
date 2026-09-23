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

        [CommandOption("--no-build-rules")]
        [Description("Leave out MagicCSharp.Analyzers, the code-style rules every project otherwise builds with")]
        [DefaultValue(false)]
        public bool NoBuildRules { get; init; }

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
        // An empty root keeps every path relative, so the output names files the way the user sees them.
        var wrote = WriteRepository("", settings);

        Output.Blank();

        if (!wrote)
        {
            Output.Note("Nothing to do — this repository is already set up.");
            return 0;
        }

        Output.Success("Ready.");
        Output.Plain("  mcs create-app --name Shop --database shop");
        Output.Plain($"  mcs create-domain --solution {settings.Prefix}.Shop.slnx --name Domains.Orders --models --tests");

        return 0;
    }

    /// <summary>
    ///     Writes whatever of the repository skeleton is missing under <paramref name="root" />. Returns whether it
    ///     wrote anything.
    /// </summary>
    internal static bool WriteRepository(string root, Settings settings)
    {
        var prefix = settings.Prefix!;

        // Defaults to the version this tool shipped with, so a new repository never pins something older
        // than its own tooling.
        var version = settings.PackageVersion ?? ToolVersion.Current;
        var buildRules = !settings.NoBuildRules;

        var renderer = new TemplateRenderer(new TemplateResolver(Path.Combine(root, TemplateResolver.DefaultOverrideDirectory)));
        var model = new { prefix, version, target_framework = settings.TargetFramework, build_rules = buildRules };

        Output.Plain($"Prefix: {prefix}   MagicCSharp: {version}   Target: {settings.TargetFramework}   Build rules: {(buildRules ? "on" : "off")}");
        Output.Blank();

        var wrote = false;

        // "templates" is written even though it matches the default, so the override mechanism is
        // discoverable from the config rather than only from documentation.
        wrote |= TemplateRenderer.Write(Path.Combine(root, RepoConfig.FileName), $$"""
            {
              "prefix": "{{prefix}}",
              "templates": "{{TemplateResolver.DefaultOverrideDirectory}}"
            }

            """);

        wrote |= renderer.Render("Repo/Directory.Build.props.hbs", Path.Combine(root, "Directory.Build.props"), model);
        wrote |= renderer.Render("Repo/Directory.Packages.props.hbs", Path.Combine(root, "Directory.Packages.props"), model);
        wrote |= TemplateRenderer.Write(Path.Combine(root, $"{prefix}.All.slnx"), "<Solution>\n</Solution>\n");

        // git does not track empty directories, so without these the two top-level directories vanish on
        // a fresh clone.
        wrote |= TemplateRenderer.Write(Path.Combine(root, "Apps", ".gitkeep"), "");
        wrote |= TemplateRenderer.Write(Path.Combine(root, "Libs", ".gitkeep"), "");

        // Without one, the first build leaves hundreds of bin/ and obj/ files staged.
        wrote |= renderer.Render("Repo/gitignore.hbs", Path.Combine(root, ".gitignore"), model);

        // Only the build rules need it: it tells them an EF migration is generated code.
        if (buildRules)
        {
            wrote |= renderer.Render("Repo/editorconfig.hbs", Path.Combine(root, ".editorconfig"), model);
        }

        return wrote;
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
