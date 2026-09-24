using MagicCSharp.Cli.Infrastructure;

namespace MagicCSharp.Cli.Tests;

/// <summary>
///     Two apps over a small graph: Leasing ships Libs/Events, Billing ships Libs/Events and Libs/Money, and only
///     Leasing's tests use Libs/Testing.
/// </summary>
public class AffectedAppsTests : IDisposable
{
    private readonly TemporaryRepository temporaryRepository = new();

    public AffectedAppsTests()
    {
        WriteProject("Libs/Events/Default/Acme.Events.csproj");
        WriteProject("Libs/Money/Default/Acme.Money.csproj");
        WriteProject("Libs/Testing/Default/Acme.Testing.csproj");
        WriteProject("Apps/Leasing/Leasing.App/Acme.Leasing.App.csproj", "../../../Libs/Events/Default/Acme.Events.csproj");
        WriteProject("Apps/Leasing/Tests/Acme.Leasing.Tests.csproj", "../Leasing.App/Acme.Leasing.App.csproj", "../../../Libs/Testing/Default/Acme.Testing.csproj");
        WriteProject("Apps/Billing/Billing.App/Acme.Billing.App.csproj", "../../../Libs/Events/Default/Acme.Events.csproj", "../../../Libs/Money/Default/Acme.Money.csproj");

        WriteSolution("Acme.Leasing.slnx", "Apps/Leasing/Leasing.App/Acme.Leasing.App.csproj", "Apps/Leasing/Tests/Acme.Leasing.Tests.csproj");
        WriteSolution("Acme.Billing.slnx", "Apps/Billing/Billing.App/Acme.Billing.App.csproj");
    }

    [Fact]
    public void A_change_inside_an_app_affects_only_that_app()
    {
        Assert.Equal(["Leasing"], Affected(false, "Apps/Leasing/Leasing.Domains/Leases/Default/SignLeaseUseCase.cs"));
    }

    [Fact]
    public void A_shared_library_affects_every_app_that_ships_it()
    {
        Assert.Equal(["Billing", "Leasing"], Affected(false, "Libs/Events/Default/LeaseSignedEvent.cs"));
        Assert.Equal(["Billing"], Affected(false, "Libs/Money/Default/Amount.cs"));
    }

    [Fact]
    public void A_file_every_build_reads_affects_every_app()
    {
        Assert.Equal(["Billing", "Leasing"], Affected(false, "Directory.Packages.props"));
    }

    [Fact]
    public void A_change_outside_every_app_and_library_affects_nothing()
    {
        Assert.Empty(Affected(false, "README.md", "docs/ci-cd.md", "Libs/Unused/Default/Thing.cs"));
    }

    [Fact]
    public void A_test_only_library_affects_the_app_only_when_its_solution_counts()
    {
        Assert.Empty(Affected(false, "Libs/Testing/Default/PostgresFixture.cs"));
        Assert.Equal(["Leasing"], Affected(true, "Libs/Testing/Default/PostgresFixture.cs"));
    }

    [Fact]
    public void A_folder_that_only_shares_a_name_prefix_does_not_match()
    {
        Assert.Empty(Affected(false, "Libs/EventsArchive/Default/Old.cs"));
    }

    [Fact]
    public void The_closure_follows_references_through_other_projects()
    {
        var folders = ProjectGraph.Folders(temporaryRepository.Root, Path.Combine(temporaryRepository.Root, "Apps/Leasing/Tests/Acme.Leasing.Tests.csproj"));

        Assert.Equal(["Apps/Leasing/Leasing.App", "Apps/Leasing/Tests", "Libs/Events/Default", "Libs/Testing/Default"], folders);
    }

    [Fact]
    public void A_reference_to_a_missing_project_fails_rather_than_under_reporting()
    {
        WriteProject("Apps/Broken/Broken.App/Acme.Broken.App.csproj", "../../../Libs/Gone/Acme.Gone.csproj");

        Assert.Throws<FileNotFoundException>(() => AppGraphs.Load(temporaryRepository.Root, "Acme", false));
    }

    public void Dispose()
    {
        temporaryRepository.Dispose();
    }

    private IReadOnlyList<string> Affected(bool shouldIncludeSolution, params string[] changedPaths)
    {
        var apps = AppGraphs.Load(temporaryRepository.Root, "Acme", shouldIncludeSolution);
        return AffectedApps.Find(apps, changedPaths);
    }

    private void WriteProject(string path, params string[] references)
    {
        var fullPath = Path.Combine(temporaryRepository.Root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var items = string.Concat(references.Select(reference => $"    <ProjectReference Include=\"{reference}\" />\n"));
        File.WriteAllText(fullPath, $"<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n{items}  </ItemGroup>\n</Project>\n");
    }

    private void WriteSolution(string name, params string[] projects)
    {
        var items = string.Concat(projects.Select(project => $"  <Project Path=\"{project}\" />\n"));
        File.WriteAllText(Path.Combine(temporaryRepository.Root, name), $"<Solution>\n{items}</Solution>\n");
    }
}
