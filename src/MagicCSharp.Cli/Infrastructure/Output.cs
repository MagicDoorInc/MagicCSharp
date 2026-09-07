using Spectre.Console;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Console writing in one place, so every command reports the same way and callers do not have to
///     remember to escape markup in a path.
/// </summary>
public static class Output
{
    public static void Created(string path)
    {
        AnsiConsole.MarkupLine($"  [green]created[/] {Markup.Escape(path)}");
    }

    public static void Updated(string path, string what)
    {
        AnsiConsole.MarkupLine($"  [green]updated[/] {Markup.Escape(path)} ({Markup.Escape(what)})");
    }

    public static void Kept(string path)
    {
        AnsiConsole.MarkupLine($"  [grey]exists, kept[/] {Markup.Escape(path)}");
    }

    public static void Referenced(string reference, string project)
    {
        AnsiConsole.MarkupLine($"  [green]referenced[/] {Markup.Escape(reference)} from {Markup.Escape(project)}");
    }

    public static void Note(string message)
    {
        AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(message)}[/]");
    }

    public static void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    public static void Hint(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
    }

    public static void Success(string message)
    {
        AnsiConsole.MarkupLine($"[bold green]{Markup.Escape(message)}[/]");
    }

    public static void Plain(string message)
    {
        AnsiConsole.WriteLine(message);
    }

    public static void Blank()
    {
        AnsiConsole.WriteLine();
    }
}
