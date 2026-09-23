namespace MagicCSharp.Cli.Infrastructure;

public class TemplateNotFoundException(string name)
    : Exception($"No template named '{name}'. Run 'mcs templates list' to see the names.")
{
    public string TemplateName { get; } = name;
}
