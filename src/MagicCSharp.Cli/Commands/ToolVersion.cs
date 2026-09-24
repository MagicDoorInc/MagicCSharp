namespace MagicCSharp.Cli.Commands;

/// <summary>The version of this tool, read from the assembly rather than hardcoded in two places.</summary>
public static class ToolVersion
{
    public static string Current { get; } = ReadCurrent();

    private static string ReadCurrent()
    {
        var version = typeof(ToolVersion).Assembly.GetName().Version;
        if (version == null)
        {
            return "0.0.0";
        }

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
