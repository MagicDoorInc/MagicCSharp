using Scriban;
using Scriban.Runtime;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Renders templates to files.
///     <para>
///         Never overwrites. Regenerating over a file someone has edited would throw the edits away, so an
///         existing file is reported and skipped; delete it if you want it back.
///     </para>
/// </summary>
public class TemplateRenderer(TemplateResolver resolver)
{
    public TemplateResolver Resolver { get; } = resolver;

    /// <summary>Renders to <paramref name="targetPath" />. Returns whether it wrote anything.</summary>
    public bool Render(string templateName, string targetPath, object model)
    {
        if (File.Exists(targetPath))
        {
            Output.Kept(targetPath);
            return false;
        }

        var rendered = RenderToString(templateName, model);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(targetPath, rendered);
        Output.Created(targetPath);
        return true;
    }

    /// <summary>Renders without writing, for tests and for previewing.</summary>
    public string RenderToString(string templateName, object model)
    {
        var template = Template.Parse(Resolver.Read(templateName));

        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"Template '{templateName}' could not be parsed: {string.Join("; ", template.Messages)}");
        }

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        return template.Render(context);
    }

    /// <summary>Writes literal content, with the same never-overwrite rule.</summary>
    public static bool Write(string targetPath, string content)
    {
        if (File.Exists(targetPath))
        {
            Output.Kept(targetPath);
            return false;
        }

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(targetPath, content);
        Output.Created(targetPath);
        return true;
    }
}
