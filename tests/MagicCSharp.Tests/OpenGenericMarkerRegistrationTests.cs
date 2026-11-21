using System.Reflection;
using MagicCSharp.Modules;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MagicCSharp.Tests;

public class OpenGenericMarkerRegistrationTests
{
    private static bool ThisAssemblyOnly(Assembly assembly)
    {
        return assembly == typeof(OpenGenericMarkerRegistrationTests).Assembly;
    }

    [Fact]
    public void AddImplementationsOf_RegistersNonGenericSubInterfacesOfAnOpenGenericMarker()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddImplementationsOf(typeof(IStore<>), new ImplementationRegistrationOptions { AssemblyFilter = ThisAssemblyOnly });

        // Assert: ICounterStore extends a closed IStore<int> and is registered; IKeyedStore<string> is itself generic,
        // a further contract rather than a service, and is not.
        using var provider = services.BuildServiceProvider();
        Assert.IsType<CounterStore>(provider.GetRequiredService<ICounterStore>());
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IKeyedStore<string>));
    }

    public interface IStore<TValue>
    {
        TValue Read();
    }

    public interface ICounterStore : IStore<int>;

    public class CounterStore : ICounterStore
    {
        public int Read()
        {
            return 1;
        }
    }

    public interface IKeyedStore<TValue> : IStore<TValue>;

    public class NameStore : IKeyedStore<string>
    {
        public string Read()
        {
            return "name";
        }
    }
}
