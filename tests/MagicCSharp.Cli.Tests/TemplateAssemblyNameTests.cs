using MagicCSharp.Cli.Infrastructure;

namespace MagicCSharp.Cli.Tests;

public class TemplateAssemblyNameTests
{
    /// <summary>
    ///     Every project template must take its assembly name from the one the command worked out, not
    ///     rebuild it from parts.
    ///     <para>
    ///         The domain template used to compose its own — <c>{{prefix}}.Libraries.{{name}}</c> — which
    ///         left the service out. Shop's Orders domain and Notifications' Orders domain would both have
    ///         built an assembly called <c>Acme.Libraries.Domains.Orders</c>, and the second to load wins.
    ///     </para>
    /// </summary>
    [Theory]
    [InlineData("Libraries/default.csproj.hbs")]
    [InlineData("Libraries/models.csproj.hbs")]
    public void Project_templates_use_the_name_the_command_computed(string template)
    {
        var resolver = new TemplateResolver(null);
        var content = resolver.Read(template);

        foreach (var line in content.Split('\n').Where(line => line.Contains("<AssemblyName>") || line.Contains("<RootNamespace>")))
        {
            Assert.Contains("assembly_name", line);
        }
    }

    [Fact]
    public void The_domain_template_produces_a_name_that_names_its_service()
    {
        var renderer = new TemplateRenderer(new TemplateResolver(null));
        var model = new
        {
            prefix = "Acme",
            name = "Domains.Orders",
            assembly_name = "Acme.Shop.Domains.Orders",
        };

        var rendered = renderer.RenderToString("Libraries/default.csproj.hbs", model);

        Assert.Contains("<AssemblyName>Acme.Shop.Domains.Orders</AssemblyName>", rendered);
        Assert.DoesNotContain("Acme.Libraries.Domains.Orders", rendered);
    }
}
