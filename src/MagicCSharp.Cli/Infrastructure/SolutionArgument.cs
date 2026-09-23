namespace MagicCSharp.Cli.Infrastructure;

/// <summary>
///     Works out which service a command was aimed at.
///     <para>
///         The commands used to demand the exact path of a solution file, so working on the only service in
///         a repository still meant typing <c>--solution Acme.Shop.slnx</c>, and a plausible
///         <c>--solution Shop</c> was rejected outright. Both now resolve, and a repository with one
///         service needs no flag at all.
///     </para>
/// </summary>
public static class SolutionArgument
{
    /// <summary>
    ///     The solution file to work on, or null with a printed explanation when there is no single answer.
    /// </summary>
    /// <param name="config">The repository config, for the prefix that solution names are built from.</param>
    /// <param name="requested">
    ///     What the user passed to <c>--solution</c>: a path, a bare service name, or nothing.
    /// </param>
    /// <param name="directory">Where to look. Defaults to the working directory, as the commands run.</param>
    public static string? Resolve(RepoConfig config, string? requested, string? directory = null)
    {
        directory ??= Directory.GetCurrentDirectory();

        var available = Available(config, directory);

        if (string.IsNullOrWhiteSpace(requested))
        {
            return Only(available);
        }

        // A path, given in full and already correct.
        if (File.Exists(Path.Combine(directory, requested)))
        {
            return requested;
        }

        // A service name: "Shop", or "Acme.Shop", or a filename that is not where we looked.
        var name = config.AppNameFromSolution(requested);
        var match = available.FirstOrDefault(solution =>
            string.Equals(config.AppNameFromSolution(solution), name, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            return match;
        }

        Output.Error($"No service called {name}.");
        List(config, available);
        return null;
    }

    /// <summary>
    ///     The service solutions in the current directory, excluding the all-services one — that exists to
    ///     open everything at once and is not what a per-service command means.
    /// </summary>
    private static IReadOnlyList<string> Available(RepoConfig config, string directory)
    {
        return Directory.GetFiles(directory, $"{config.Prefix}.*.slnx")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(solution => !string.Equals(config.AppNameFromSolution(solution), "All", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string? Only(IReadOnlyList<string> available)
    {
        if (available.Count == 1)
        {
            return available[0];
        }

        if (available.Count == 0)
        {
            Output.Error("No services in this repository yet.");
            Output.Hint("Create one first:");
            Output.Plain("  mcs create-app --name Shop --database shop");
            return null;
        }

        Output.Error("More than one service here, so say which. Use --solution <SERVICE>");
        Output.Blank();

        foreach (var solution in available)
        {
            Output.Plain($"  {solution}");
        }

        return null;
    }

    private static void List(RepoConfig config, IReadOnlyList<string> available)
    {
        if (available.Count == 0)
        {
            Output.Hint($"There are no services here. Create one: mcs create-app --name Shop --database shop");
            return;
        }

        Output.Hint("Services in this repository:");

        foreach (var solution in available)
        {
            Output.Plain($"  {config.AppNameFromSolution(solution)}   ({solution})");
        }
    }
}
