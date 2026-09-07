using System.Reflection;
using MagicCSharp.Modules;
using MagicCSharp.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MagicCSharp.Tests;

public class ImplementationRegistrationModuleTests
{
    /// <summary>
    ///     Every test scans only this assembly. Without the filter the scan would also find the marker types
    ///     other tests declare, and each test's expectations would depend on what the rest of the file contains.
    /// </summary>
    private static bool ThisAssemblyOnly(Assembly assembly)
    {
        return assembly == typeof(ImplementationRegistrationModuleTests).Assembly;
    }

    [Fact]
    public void Registers_the_implementation_behind_its_own_interface()
    {
        var services = new ServiceCollection();

        services.AddImplementationsOf<ISingleMarker>(assemblyFilter: ThisAssemblyOnly);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<OnlyImplementation>(provider.GetRequiredService<IOnlyUseCase>());
    }

    [Fact]
    public void Throws_when_an_interface_extends_the_marker_but_nothing_implements_it()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddImplementationsOf<IOrphanMarker>(assemblyFilter: ThisAssemblyOnly));

        Assert.Contains(nameof(IOrphanUseCase), ex.Message);
    }

    [Fact]
    public void Throws_when_two_classes_implement_the_same_interface()
    {
        // Registering both would make GetRequiredService silently return whichever came last, which is the
        // failure this replaces: a test double in the container, resolved in production, with no signal.
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddImplementationsOf<IAmbiguousMarker>(assemblyFilter: ThisAssemblyOnly));

        Assert.Contains(nameof(FirstAmbiguous), ex.Message);
        Assert.Contains(nameof(SecondAmbiguous), ex.Message);
    }

    [Fact]
    public void Registers_both_when_multiple_implementations_are_allowed()
    {
        var services = new ServiceCollection();

        services.AddImplementationsOf<IAmbiguousMarker>(assemblyFilter: ThisAssemblyOnly, allowMultipleImplementations: true);

        using var provider = services.BuildServiceProvider();
        Assert.Equal(2, provider.GetServices<IAmbiguousUseCase>().Count());
    }

    [Fact]
    public void Attribute_overrides_the_default_lifetime()
    {
        var services = new ServiceCollection();

        services.AddMagicUseCases(ThisAssemblyOnly);

        var scoped = services.Single(d => d.ServiceType == typeof(IScopedByDefaultUseCase));
        var singleton = services.Single(d => d.ServiceType == typeof(ISingletonUseCase));

        Assert.Equal(ServiceLifetime.Scoped, scoped.Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, singleton.Lifetime);
    }

    [Fact]
    public void Lazy_defers_construction_until_the_value_is_read()
    {
        var services = new ServiceCollection();
        services.AddMagicUseCases(ThisAssemblyOnly);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        CountingUseCase.Constructions = 0;

        var lazy = scope.ServiceProvider.GetRequiredService<Lazy<ICountingUseCase>>();
        Assert.Equal(0, CountingUseCase.Constructions);

        _ = lazy.Value;
        Assert.Equal(1, CountingUseCase.Constructions);
    }

    [Fact]
    public void Lazy_resolves_a_scoped_use_case_from_the_scope_that_asked_for_it()
    {
        // A singleton Lazy<T> would capture the root provider and hand every scope the same instance of a
        // scoped service — a captive dependency that only shows up under concurrent requests.
        var services = new ServiceCollection();
        services.AddMagicUseCases(ThisAssemblyOnly);

        using var provider = services.BuildServiceProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var fromFirst = first.ServiceProvider.GetRequiredService<Lazy<IScopedByDefaultUseCase>>().Value;
        var fromSecond = second.ServiceProvider.GetRequiredService<Lazy<IScopedByDefaultUseCase>>().Value;
        var againFromFirst = first.ServiceProvider.GetRequiredService<IScopedByDefaultUseCase>();

        Assert.NotSame(fromFirst, fromSecond);
        Assert.Same(fromFirst, againFromFirst);
    }

    [Fact]
    public void AddImplementationsOfBase_collects_the_whole_catalog_under_the_base_type()
    {
        var services = new ServiceCollection();

        services.AddImplementationsOfBase<Rule>(typeof(ImplementationRegistrationModuleTests).Assembly);

        using var provider = services.BuildServiceProvider();
        var rules = provider.GetServices<Rule>().ToList();

        Assert.Equal(2, rules.Count);
        Assert.Contains(rules, rule => rule is FirstRule);
        Assert.Contains(rules, rule => rule is SecondRule);
    }
}

// ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────────

public interface ISingleMarker;

public interface IOnlyUseCase : ISingleMarker;

public class OnlyImplementation : IOnlyUseCase;

public interface IOrphanMarker;

public interface IOrphanUseCase : IOrphanMarker;

public interface IAmbiguousMarker;

public interface IAmbiguousUseCase : IAmbiguousMarker;

public class FirstAmbiguous : IAmbiguousUseCase;

public class SecondAmbiguous : IAmbiguousUseCase;

public interface IScopedByDefaultUseCase : IMagicUseCase;

public class ScopedByDefaultUseCase : IScopedByDefaultUseCase;

public interface ISingletonUseCase : IMagicUseCase;

[MagicUseCase(ServiceLifetime.Singleton)]
public class SingletonUseCase : ISingletonUseCase;

public interface ICountingUseCase : IMagicUseCase;

public class CountingUseCase : ICountingUseCase
{
    public CountingUseCase()
    {
        Constructions++;
    }

    public static int Constructions { get; set; }
}

public abstract class Rule;

public class FirstRule : Rule;

public class SecondRule : Rule;
