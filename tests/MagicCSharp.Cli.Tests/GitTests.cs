using MagicCSharp.Cli.Infrastructure;

namespace MagicCSharp.Cli.Tests;

public class GitTests
{
    [Fact]
    public void A_path_outside_any_repository_is_not_reported_as_ignored()
    {
        // git exits 128 there. Treating that as "ignored" would warn on every eject done outside a
        // repository, which is exactly how a shared template repository starts out.
        using var repo = new TempRepo();

        Assert.False(Git.IsIgnored(Path.Combine(repo.Root, "anything.hbs")));
    }

    [Fact]
    public void A_missing_git_does_not_throw()
    {
        // Whatever the environment, asking must not take the command down with it.
        var exception = Record.Exception(() => Git.IsIgnored("some/path.hbs"));

        Assert.Null(exception);
    }
}
