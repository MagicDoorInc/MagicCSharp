using MagicCSharp.Cli.Commands;

namespace MagicCSharp.Cli.Tests;

public class InitTests
{
    [Fact]
    public void A_new_repository_builds_with_the_analyzers_at_the_pinned_version()
    {
        using var temporaryRepository = new TemporaryRepository();

        InitCommand.WriteRepository(temporaryRepository.Root, new InitCommand.Settings { Prefix = "Acme", PackageVersion = "1.2.3" });

        var buildProps = File.ReadAllText(Path.Combine(temporaryRepository.Root, "Directory.Build.props"));
        var packagesProps = File.ReadAllText(Path.Combine(temporaryRepository.Root, "Directory.Packages.props"));
        Assert.Contains("<PackageReference Include=\"MagicCSharp.Analyzers\" PrivateAssets=\"all\" />", buildProps);
        Assert.Contains("<PackageVersion Include=\"MagicCSharp.Analyzers\" Version=\"1.2.3\" />", packagesProps);
        Assert.Contains("generated_code = true", File.ReadAllText(Path.Combine(temporaryRepository.Root, ".editorconfig")));
    }

    [Fact]
    public void No_build_rules_leaves_the_analyzers_out()
    {
        using var temporaryRepository = new TemporaryRepository();

        InitCommand.WriteRepository(temporaryRepository.Root, new InitCommand.Settings { Prefix = "Acme", PackageVersion = "1.2.3", ShouldSkipBuildRules = true });

        Assert.DoesNotContain("MagicCSharp.Analyzers", File.ReadAllText(Path.Combine(temporaryRepository.Root, "Directory.Build.props")));
        Assert.DoesNotContain("MagicCSharp.Analyzers", File.ReadAllText(Path.Combine(temporaryRepository.Root, "Directory.Packages.props")));
        Assert.False(File.Exists(Path.Combine(temporaryRepository.Root, ".editorconfig")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Running_init_twice_changes_nothing(bool shouldSkipBuildRules)
    {
        using var temporaryRepository = new TemporaryRepository();
        var settings = new InitCommand.Settings { Prefix = "Acme", PackageVersion = "1.2.3", ShouldSkipBuildRules = shouldSkipBuildRules };
        InitCommand.WriteRepository(temporaryRepository.Root, settings);
        var firstRun = Snapshot(temporaryRepository.Root);

        var hasWrittenAgain = InitCommand.WriteRepository(temporaryRepository.Root, settings);

        Assert.False(hasWrittenAgain);
        Assert.Equal(firstRun, Snapshot(temporaryRepository.Root));
    }

    private static Dictionary<string, string> Snapshot(string root)
    {
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllText);
    }
}
