using System.Reflection;
using MagicCSharp.Modules;
using Xunit;

namespace MagicCSharp.Tests;

public class ApplicationAssembliesTests
{
    [Fact]
    public void Finds_a_referenced_assembly_whose_types_nothing_has_touched()
    {
        // The bug this guards against: a domain project holding only event handlers is referenced by the
        // host and used by nothing, so .NET has not loaded it when discovery runs and its handlers are
        // silently never registered. Any assembly this test project references but does not use stands in
        // for that — the reference exists on disk, so the walk must find it whether or not it is loaded.
        var referenced = Assembly.GetExecutingAssembly()
            .GetReferencedAssemblies()
            .Select(name => name.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var found = ApplicationAssemblies.All()
            .Select(assembly => assembly.GetName().Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var missing = referenced
            .Where(name => name.StartsWith("MagicCSharp", StringComparison.Ordinal))
            .Where(name => !found.Contains(name))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Framework_assemblies_are_not_application_code()
    {
        Assert.False(ApplicationAssemblies.IsApplicationAssembly(typeof(string).Assembly));
    }

    [Fact]
    public void Your_own_assembly_is_application_code()
    {
        Assert.True(ApplicationAssemblies.IsApplicationAssembly(typeof(ApplicationAssemblies).Assembly));
    }

    [Fact]
    public void Loading_twice_is_harmless()
    {
        ApplicationAssemblies.EnsureLoaded();
        var first = ApplicationAssemblies.All().Count;

        ApplicationAssemblies.EnsureLoaded();

        Assert.Equal(first, ApplicationAssemblies.All().Count);
    }
}
