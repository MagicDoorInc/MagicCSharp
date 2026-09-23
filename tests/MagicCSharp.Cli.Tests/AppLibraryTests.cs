using MagicCSharp.Cli.Infrastructure;

namespace MagicCSharp.Cli.Tests;

public class AppLibraryTests
{
    private static readonly RepoConfig Config = new RepoConfig { Prefix = "Acme" };

    private static AppLibrary Plan(string name)
    {
        var library = AppLibrary.Plan(Config, "Shop", name);

        Assert.NotNull(library);
        return library;
    }

    [Theory]
    [InlineData("Processors", "Apps/Shop/Shop.Processors")]
    [InlineData("Domains.Orders", "Apps/Shop/Shop.Domains/Orders")]
    [InlineData("Domains.Orders.App", "Apps/Shop/Shop.Domains/Orders/App")]
    [InlineData("Domains.Orders.Fulfilment", "Apps/Shop/Shop.Domains/Orders/Fulfilment")]
    [InlineData("Domains.Orders.Fulfilment.App", "Apps/Shop/Shop.Domains/Orders/Fulfilment/App")]
    [InlineData("Clients.Stripe", "Apps/Shop/Shop.Clients/Stripe")]
    public void The_first_segment_joins_the_service_name_and_the_rest_nest(string name, string directory)
    {
        Assert.Equal(directory, Plan(name).Directory);
    }

    [Theory]
    [InlineData("Processors", "Acme.Shop.Processors")]
    [InlineData("Domains.Orders", "Acme.Shop.Domains.Orders")]
    [InlineData("Domains.Orders.App", "Acme.Shop.Domains.Orders.App")]
    public void The_assembly_keeps_every_dot_and_names_the_service(string name, string assembly)
    {
        Assert.Equal(assembly, Plan(name).AssemblyName);
    }

    [Fact]
    public void The_three_projects_sit_under_the_library_directory()
    {
        var library = Plan("Domains.Orders");

        Assert.Equal("Apps/Shop/Shop.Domains/Orders/Default/Acme.Shop.Domains.Orders.csproj", library.DefaultProject);
        Assert.Equal("Apps/Shop/Shop.Domains/Orders/Models/Acme.Shop.Domains.Orders.Models.csproj", library.ModelsProject);
        Assert.Equal("Apps/Shop/Shop.Domains/Orders/Tests/Acme.Shop.Domains.Orders.Tests.csproj", library.TestsProject);
    }

    [Theory]
    [InlineData("Domains.Orders", true)]
    [InlineData("Domains.Orders.App", true)]
    [InlineData("Processors", false)]
    [InlineData("Clients.Stripe", false)]
    public void Only_what_sits_under_Domains_is_wired_to_the_host(string name, bool isDomain)
    {
        // An assembly nothing references is not deployed, so its use cases never register. That is why a
        // domain is wired and an app library is left to a domain to reference.
        Assert.Equal(isDomain, Plan(name).IsDomain);
    }

    [Theory]
    [InlineData("Domains.Orders.App", true)]
    [InlineData("Domains.Orders.Fulfilment.App", true)]
    [InlineData("Domains.Orders", false)]
    [InlineData("Domains.Application", false)]
    public void A_last_segment_of_App_is_an_http_surface(string name, bool isApp)
    {
        Assert.Equal(isApp, Plan(name).IsHttpSurface);
    }

    [Fact]
    public void An_http_surface_is_rendered_from_its_own_template()
    {
        Assert.Equal("Libraries/app.csproj.hbs", Plan("Domains.Orders.App").DefaultTemplate);
        Assert.Equal("Libraries/default.csproj.hbs", Plan("Domains.Orders").DefaultTemplate);
    }

    [Fact]
    public void A_nested_library_knows_its_parent()
    {
        Assert.Equal(
            "Apps/Shop/Shop.Domains/Orders/Default/Acme.Shop.Domains.Orders.csproj",
            Plan("Domains.Orders.App").ParentDefaultProject);

        Assert.Equal(
            "Apps/Shop/Shop.Domains/Orders/Fulfilment/Default/Acme.Shop.Domains.Orders.Fulfilment.csproj",
            Plan("Domains.Orders.Fulfilment.App").ParentDefaultProject);
    }

    [Fact]
    public void A_single_segment_library_has_no_parent()
    {
        Assert.Null(Plan("Processors").ParentDefaultProject);
    }

    [Fact]
    public void The_host_project_is_the_services_own_app()
    {
        Assert.Equal("Apps/Shop/Shop.App/Acme.Shop.App.csproj", Plan("Domains.Orders").HostProject);
    }

    [Theory]
    [InlineData("Domains.Orders", "Orders")]
    [InlineData("Domains.Orders.Fulfilment", "Orders.Fulfilment")]
    public void Add_entity_is_told_the_domain_without_the_container(string name, string domain)
    {
        Assert.Equal(domain, Plan(name).EntityDomain);
    }

    [Theory]
    [InlineData("Domains.Orders.App")]
    [InlineData("Processors")]
    public void Entities_do_not_belong_in_an_http_surface_or_an_app_library(string name)
    {
        Assert.Null(Plan(name).EntityDomain);
    }

    [Theory]
    [InlineData("Domain.Orders")]           // singular: a sibling of Domains/ the host never references
    [InlineData("Domians.Orders")]          // transposed
    [InlineData("Domain")]
    [InlineData("Domans.Orders")]
    [InlineData("Domainss.Orders")]
    public void A_near_miss_of_Domains_is_refused_rather_than_silently_unwired(string name)
    {
        // Shop.Domain/Orders builds and ships and does nothing, because only Shop.Domains/ is referenced
        // by the host. One letter should not be the difference between working and silently not.
        Assert.Null(AppLibrary.Plan(Config, "Shop", name));
    }

    [Theory]
    [InlineData("Processors")]
    [InlineData("Clients.Stripe")]
    [InlineData("Testing.Fixtures")]
    [InlineData("Documents.Templates")]
    [InlineData("Mains.Something")]
    public void A_name_that_is_not_close_to_Domains_is_left_alone(string name)
    {
        Assert.NotNull(AppLibrary.Plan(Config, "Shop", name));
    }

    [Theory]
    [InlineData("Domains.Orders.Models")]   // would collide with the Models project of Orders
    [InlineData("Domains.Orders.Default")]
    [InlineData("Domains.Orders.Tests")]
    [InlineData("App")]                     // duplicates the host's assembly name
    [InlineData("App.Something")]
    [InlineData("Data")]                    // persistence is one place per service
    [InlineData("Data.Extra")]
    [InlineData("Domains")]                 // the container, not a domain
    [InlineData("Domains.App.Orders")]      // App is only ever the last segment
    [InlineData("not-pascal")]
    [InlineData("lowercase")]
    public void Names_that_would_produce_a_broken_layout_are_refused(string name)
    {
        Assert.Null(AppLibrary.Plan(Config, "Shop", name));
    }
}
