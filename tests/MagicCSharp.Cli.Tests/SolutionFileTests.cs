using MagicCSharp.Cli.Infrastructure;

namespace MagicCSharp.Cli.Tests;

public class SolutionFileTests
{
    [Fact]
    public void AddProjects_writes_them_sorted()
    {
        using var repo = new TempRepo();
        var path = Path.Combine(repo.Root, "Acme.Shop.slnx");

        SolutionFile.AddProjects(path, ["Apps/Shop/B/B.csproj", "Apps/Shop/A/A.csproj"]);

        Assert.Equal(["Apps/Shop/A/A.csproj", "Apps/Shop/B/B.csproj"], SolutionFile.ReadProjects(path));
    }

    [Fact]
    public void AddProjects_keeps_what_is_already_there_and_does_not_duplicate()
    {
        using var repo = new TempRepo();
        var path = Path.Combine(repo.Root, "Acme.Shop.slnx");

        SolutionFile.AddProjects(path, ["A/A.csproj"]);
        var changed = SolutionFile.AddProjects(path, ["A/A.csproj", "B/B.csproj"]);

        Assert.True(changed);
        Assert.Equal(["A/A.csproj", "B/B.csproj"], SolutionFile.ReadProjects(path));
    }

    [Fact]
    public void AddProjects_reports_no_change_when_nothing_is_new()
    {
        using var repo = new TempRepo();
        var path = Path.Combine(repo.Root, "Acme.Shop.slnx");

        SolutionFile.AddProjects(path, ["A/A.csproj"]);
        var before = File.ReadAllText(path);

        var changed = SolutionFile.AddProjects(path, ["A/A.csproj"]);

        Assert.False(changed);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void WriteGrouped_nests_by_the_first_two_path_segments()
    {
        using var repo = new TempRepo();
        var path = Path.Combine(repo.Root, "Acme.All.slnx");

        SolutionFile.WriteGrouped(path, ["Apps/Shop/A/A.csproj", "Apps/Shop/B/B.csproj", "Libs/Events/Default/E.csproj"]);

        var content = File.ReadAllText(path);

        Assert.Contains("""<Folder Name="/Apps/" />""", content);
        Assert.Contains("""<Folder Name="/Apps/Shop/">""", content);
        Assert.Contains("""<Folder Name="/Libs/Events/">""", content);
    }

    [Fact]
    public void WriteGrouped_is_byte_identical_when_nothing_changed()
    {
        // This is what makes `sync` produce no diff on a no-op run.
        using var repo = new TempRepo();
        var path = Path.Combine(repo.Root, "Acme.All.slnx");
        string[] projects = ["Apps/Shop/A/A.csproj"];

        SolutionFile.WriteGrouped(path, projects);
        var first = File.ReadAllText(path);

        var changed = SolutionFile.WriteGrouped(path, projects);

        Assert.False(changed);
        Assert.Equal(first, File.ReadAllText(path));
    }

    [Fact]
    public void DiscoverProjects_skips_build_output_and_hidden_directories()
    {
        using var repo = new TempRepo();
        foreach (var relative in new[]
                 {
                     "Apps/Shop/Shop.csproj",
                     "Apps/Shop/bin/Debug/Ghost.csproj",
                     "Apps/Shop/obj/Ghost.csproj",
                     ".magiccsharp/templates/Ghost.csproj",
                 })
        {
            var full = Path.Combine(repo.Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "<Project/>");
        }

        Assert.Equal(["Apps/Shop/Shop.csproj"], SolutionFile.DiscoverProjects(repo.Root));
    }
}
