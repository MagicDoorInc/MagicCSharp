using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MagicCSharp.Testing;

/// <summary>
///     A small dependency-injection container for one test: the fixture adds each fake once, and use cases are
///     resolved by their interface with whatever they depend on, as in the running app. Adding a dependency to a use
///     case then changes no fixture.
///     <para>
///         The test's clock is the only <see cref="System.TimeProvider" />; loggers write nothing unless a registration
///         says otherwise. The container is built by the first <see cref="Get{TService}" /> or
///         <see cref="Create{TImplementation}" />, and registrations close then, so a fake added later can't be
///         silently ignored. A missing fake shows up as the container's resolution error, naming it, on that first call.
///     </para>
/// </summary>
public class TestServices
{
    private readonly ServiceCollection serviceCollection = new ServiceCollection();

    private IServiceScope? serviceScope;

    public TestServices(TimeProvider timeProvider)
    {
        serviceCollection.AddSingleton(timeProvider);
    }

    /// <summary>Registers one instance under <typeparamref name="TService" />; a later fake of the same service replaces it for <see cref="Get{TService}" />.</summary>
    /// <exception cref="InvalidOperationException">The container was already built.</exception>
    public TestServices AddFake<TService>(TService fake)
        where TService : class
    {
        ThrowIfBuilt();
        serviceCollection.AddSingleton(fake);

        return this;
    }

    /// <summary>Any other registration, such as the app's use case discovery: <c>services.AddMagicUseCases(assembly => …)</c>.</summary>
    /// <exception cref="InvalidOperationException">The container was already built.</exception>
    public TestServices AddRegistrations(Action<IServiceCollection> addRegistrations)
    {
        ThrowIfBuilt();
        addRegistrations(serviceCollection);

        return this;
    }

    /// <summary>The registered service, from the one scope this container holds; the first call builds the container.</summary>
    public TService Get<TService>()
        where TService : notnull
    {
        return BuildOnce().ServiceProvider.GetRequiredService<TService>();
    }

    /// <summary>
    ///     A new <typeparamref name="TImplementation" /> whose constructor takes each given object in the parameter its
    ///     type fits, and everything else from the container. Only this instance gets them: a use case it depends on
    ///     still gets the registered fakes, and <see cref="Get{TService}" /> is unchanged.
    /// </summary>
    public TImplementation Create<TImplementation>(params object[] dependencies)
        where TImplementation : class
    {
        return ActivatorUtilities.CreateInstance<TImplementation>(BuildOnce().ServiceProvider, dependencies);
    }

    private IServiceScope BuildOnce()
    {
        if (serviceScope == null)
        {
            serviceCollection.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(NullLogger<>)));
            serviceCollection.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            serviceScope = serviceCollection.BuildServiceProvider().CreateScope();
        }

        return serviceScope;
    }

    private void ThrowIfBuilt()
    {
        if (serviceScope != null)
        {
            throw new InvalidOperationException(
                "Registrations are closed once the first service is resolved. Add every fake and registration before the first Get or Create.");
        }
    }
}
