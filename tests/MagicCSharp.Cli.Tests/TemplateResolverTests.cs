using MagicCSharp.Cli.Infrastructure;

namespace MagicCSharp.Cli.Tests;

public class TemplateResolverTests
{
    [Fact]
    public void Every_template_the_generators_ask_for_is_embedded()
    {
        // The eight *.cs.hbs templates once vanished into a Czech satellite assembly, because MSBuild reads
        // the .cs in "entity.cs.hbs" as a culture. The build reported all nineteen; only eleven shipped.
        // This is the test that would have caught it.
        string[] required =
        [
            "Repo/Directory.Build.props.hbs", "Repo/Directory.Packages.props.hbs", "Repo/editorconfig.hbs",
            "Apps/app.csproj.hbs", "Apps/Program.cs.hbs", "Apps/HelloController.cs.hbs",
            "Apps/appsettings.json.hbs", "Apps/appsettings.Development.json.hbs", "Apps/launchSettings.json.hbs",
            "Apps/Data/DataModels.csproj.hbs", "Apps/Data/DataEntityFramework.csproj.hbs",
            "Apps/Data/MagicContext.cs.hbs", "Apps/Data/MagicContextFactory.cs.hbs", "Apps/Data/RepositoriesModule.cs.hbs",
            "Libraries/default.csproj.hbs", "Libraries/models.csproj.hbs", "Libraries/tests.csproj.hbs",
            "Libraries/app.csproj.hbs",
            "Entities/entity.cs.hbs", "Entities/dal.cs.hbs",
            "Entities/ef-repository.cs.hbs", "Entities/repository-interface.cs.hbs",
        ];

        var embedded = TemplateResolver.BuiltInNames();

        Assert.Empty(required.Except(embedded));
    }

    [Fact]
    public void A_repository_override_wins_over_the_built_in()
    {
        using var temporaryRepository = new TemporaryRepository();
        temporaryRepository.WriteOverride("Entities/entity.cs.hbs", "OVERRIDDEN");

        var templateResolver = new TemplateResolver(temporaryRepository.OverrideDirectory);

        Assert.Equal("OVERRIDDEN", templateResolver.Read("Entities/entity.cs.hbs"));
    }

    [Fact]
    public void Overriding_one_template_leaves_the_others_built_in()
    {
        using var temporaryRepository = new TemporaryRepository();
        temporaryRepository.WriteOverride("Entities/entity.cs.hbs", "OVERRIDDEN");

        var templateResolver = new TemplateResolver(temporaryRepository.OverrideDirectory);

        Assert.Equal("OVERRIDDEN", templateResolver.Read("Entities/entity.cs.hbs"));
        Assert.NotEqual("OVERRIDDEN", templateResolver.Read("Entities/dal.cs.hbs"));
        Assert.True(templateResolver.Describe("Entities/entity.cs.hbs").IsOverride);
        Assert.False(templateResolver.Describe("Entities/dal.cs.hbs").IsOverride);
    }

    [Fact]
    public void Overrides_can_be_turned_off_entirely()
    {
        using var temporaryRepository = new TemporaryRepository();
        temporaryRepository.WriteOverride("Entities/entity.cs.hbs", "OVERRIDDEN");

        var templateResolver = new TemplateResolver(null);

        Assert.NotEqual("OVERRIDDEN", templateResolver.Read("Entities/entity.cs.hbs"));
        Assert.Null(templateResolver.OverridePath("Entities/entity.cs.hbs"));
    }

    [Fact]
    public void An_unknown_template_says_so_by_name()
    {
        var templateResolver = new TemplateResolver(null);

        var exception = Assert.Throws<TemplateNotFoundException>(() => templateResolver.Read("Nope/missing.hbs"));

        Assert.Contains("Nope/missing.hbs", exception.Message);
    }

    [Fact]
    public void Config_without_a_templates_key_gets_the_default_directory()
    {
        var templateResolver = TemplateResolver.ForRepository(new RepositoryConfig { Prefix = "Acme" });

        Assert.Equal(TemplateResolver.DefaultOverrideDirectory, templateResolver.OverrideDirectory);
    }

    [Fact]
    public void An_empty_templates_key_turns_overrides_off()
    {
        var templateResolver = TemplateResolver.ForRepository(new RepositoryConfig { Prefix = "Acme", Templates = "" });

        Assert.Null(templateResolver.OverrideDirectory);
    }

    [Fact]
    public void An_override_path_sits_under_the_configured_directory()
    {
        // The path eject writes to, and the one the commit advice names, have to be the same one the
        // generators read back.
        var templateResolver = new TemplateResolver(".magiccsharp/templates");

        var path = templateResolver.OverridePath("Entities/dal.cs.hbs");

        Assert.Equal(Path.Combine(".magiccsharp", "templates", "Entities", "dal.cs.hbs"), path);
    }

    [Fact]
    public void Every_built_in_template_parses_and_renders()
    {
        // A template with a syntax error would otherwise only surface when someone happened to run the
        // command that uses it.
        var templateRenderer = new TemplateRenderer(new TemplateResolver(null));

        var model = new
        {
            prefix = "Acme", name = "Shop", version = "1.0.0", target_framework = "net10.0", build_rules = true,
            port = 5200, assembly_name = "Acme.Shop.Domains.Orders",
            app_name = "Shop", entity_name = "Order", entity_variable = "order", plural = "Orders", table_name = "orders",
            use_key = false, is_paginated = true, entity_namespace = "Acme.Shop.Domains.Orders.Models.Entities",
            database = new { enabled = true, name = "shop" },
        };

        foreach (var name in TemplateResolver.BuiltInNames())
        {
            var rendered = templateRenderer.RenderToString(name, model);
            Assert.False(string.IsNullOrWhiteSpace(rendered), $"{name} rendered empty");
        }
    }
}
