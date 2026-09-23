using MagicCSharp.Cli.Infrastructure;
using Xunit;

namespace MagicCSharp.Cli.Tests;

public class SolutionArgumentTests : IDisposable
{
    private static readonly RepoConfig Config = new RepoConfig { Prefix = "Acme" };

    private readonly string directory = Directory.CreateTempSubdirectory("mcs-solution-").FullName;

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void One_service_needs_no_flag()
    {
        Given("Acme.Shop.slnx");

        Assert.Equal("Acme.Shop.slnx", Resolve(null));
    }

    [Fact]
    public void The_all_services_solution_does_not_count_as_a_service()
    {
        // Acme.All.slnx exists to open everything at once. Without this it would make every repository
        // look ambiguous, and asking for "All" would scaffold into Apps/All.
        Given("Acme.Shop.slnx", "Acme.All.slnx");

        Assert.Equal("Acme.Shop.slnx", Resolve(null));
    }

    [Fact]
    public void A_service_name_is_enough()
    {
        Given("Acme.Shop.slnx", "Acme.Notifications.slnx");

        Assert.Equal("Acme.Notifications.slnx", Resolve("Notifications"));
    }

    [Fact]
    public void The_full_file_name_still_works()
    {
        Given("Acme.Shop.slnx", "Acme.Notifications.slnx");

        Assert.Equal("Acme.Shop.slnx", Resolve("Acme.Shop.slnx"));
    }

    [Fact]
    public void Case_does_not_matter()
    {
        Given("Acme.Notifications.slnx");

        Assert.Equal("Acme.Notifications.slnx", Resolve("notifications"));
    }

    [Fact]
    public void Several_services_and_no_flag_is_a_refusal_not_a_guess()
    {
        Given("Acme.Shop.slnx", "Acme.Notifications.slnx");

        Assert.Null(Resolve(null));
    }

    [Fact]
    public void An_unknown_service_is_a_refusal()
    {
        Given("Acme.Shop.slnx");

        Assert.Null(Resolve("Warehouse"));
    }

    [Fact]
    public void No_services_at_all_is_a_refusal()
    {
        Assert.Null(Resolve(null));
    }

    private string? Resolve(string? requested)
    {
        return SolutionArgument.Resolve(Config, requested, directory);
    }

    private void Given(params string[] solutions)
    {
        foreach (var solution in solutions)
        {
            File.WriteAllText(Path.Combine(directory, solution), "<Solution />");
        }
    }
}
