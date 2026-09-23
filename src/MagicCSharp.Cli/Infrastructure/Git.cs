using System.Diagnostics;

namespace MagicCSharp.Cli.Infrastructure;

/// <summary>Just enough git to warn about things that would not reach a teammate.</summary>
public static class Git
{
    /// <summary>
    ///     Whether git would ignore this path.
    ///     <para>
    ///         A template override that is ignored works perfectly for the person who wrote it and reaches
    ///         nobody else, which is the kind of thing that goes unnoticed for months. Not being in a git
    ///         repository at all counts as not ignored — there is nothing to warn about yet.
    ///     </para>
    /// </summary>
    public static bool IsIgnored(string path)
    {
        try
        {
            var info = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            info.ArgumentList.Add("check-ignore");
            info.ArgumentList.Add("-q");
            info.ArgumentList.Add(path);

            using var process = Process.Start(info);

            if (process == null)
            {
                return false;
            }

            process.WaitForExit();

            // 0 means ignored, 1 means not, 128 means not a git repository.
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            // git missing, or not on PATH. Not worth failing an eject over.
            return false;
        }
    }
}
