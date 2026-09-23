using MagicCSharp.Cli.Commands;

namespace MagicCSharp.Cli.Tests;

public class InitTests
{
    [Fact]
    public void A_new_repository_builds_with_the_analyzers_at_the_pinned_version()
    {
        using var repo = new TempRepo();

        InitCommand.WriteRepository(repo.Root, new InitCommand.Settings { Prefix = "Acme", PackageVersion = "1.2.3" });

        var buildProps = File.ReadAllText(Path.Combine(repo.Root, "Directory.Build.props"));
        var packagesProps = File.ReadAllText(Path.Combine(repo.Root, "Directory.Packages.props"));
        Assert.Contains("<PackageReference Include=\"MagicCSharp.Analyzers\" PrivateAssets=\"all\" />", buildProps);
        Assert.Contains("<PackageVersion Include=\"MagicCSharp.Analyzers\" Version=\"1.2.3\" />", packagesProps);
        Assert.Contains("generated_code = true", File.ReadAllText(Path.Combine(repo.Root, ".editorconfig")));
    }

    [Fact]
    public void No_build_rules_leaves_the_analyzers_out()
    {
        using var repo = new TempRepo();

        InitCommand.WriteRepository(repo.Root, new InitCommand.Settings { Prefix = "Acme", PackageVersion = "1.2.3", NoBuildRules = true });

        Assert.DoesNotContain("MagicCSharp.Analyzers", File.ReadAllText(Path.Combine(repo.Root, "Directory.Build.props")));
        Assert.DoesNotContain("MagicCSharp.Analyzers", File.ReadAllText(Path.Combine(repo.Root, "Directory.Packages.props")));
        Assert.False(File.Exists(Path.Combine(repo.Root, ".editorconfig")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Running_init_twice_changes_nothing(bool noBuildRules)
    {
        using var repo = new TempRepo();
        var settings = new InitCommand.Settings { Prefix = "Acme", PackageVersion = "1.2.3", NoBuildRules = noBuildRules };
        InitCommand.WriteRepository(repo.Root, settings);
        var firstRun = Snapshot(repo.Root);

        var wroteAgain = InitCommand.WriteRepository(repo.Root, settings);

        Assert.False(wroteAgain);
        Assert.Equal(firstRun, Snapshot(repo.Root));
    }

    private static Dictionary<string, string> Snapshot(string root)
    {
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllText);
    }
}
