using System.Diagnostics;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Runs <c>dotnet</c> for the things it does better than editing a .csproj by hand — resolving whether a
///     reference is already there, and adding one with the right relative path.
/// </summary>
public static class DotnetCli
{
    /// <summary>Adds a project reference unless it is already present. Returns whether it added one.</summary>
    public static bool EnsureReference(string project, string reference)
    {
        var existing = Run("list", project, "reference");

        if (existing.Output.Contains(Path.GetFileName(reference), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var result = Run("add", project, "reference", reference);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not reference {Path.GetFileName(reference)} from {Path.GetFileName(project)}: {result.Error.Trim()}");
        }

        Output.Referenced(Path.GetFileName(reference), Path.GetFileName(project));
        return true;
    }

    /// <summary>Removes a project reference, used to undo a partial change.</summary>
    public static void RemoveReference(string project, string reference)
    {
        Run("remove", project, "reference", reference);
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] arguments)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException("Could not start dotnet. Is the SDK on PATH?");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output, error);
    }
}
