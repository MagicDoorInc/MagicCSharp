using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MagicCSharp.Modules;

/// <summary>
///     Convention-based dependency injection registration.
///     <para>
///         Two discovery shapes are supported. <see cref="AddImplementationsOf{TInterface}" /> registers each
///         implementation under the <em>sub-interface</em> that extends a marker (the use case and event handler
///         shape: <c>ICreateOrderUseCase : IMagicUseCase</c> resolves to <c>CreateOrderUseCase</c>).
///         <see cref="AddImplementationsOfBase{TBase}" /> registers every implementation under the marker
///         <em>itself</em>, so <c>IEnumerable&lt;TBase&gt;</c> resolves the whole catalog.
///     </para>
/// </summary>
public static class ImplementationRegistrationModule
{
    /// <summary>
    ///     Assemblies whose types can never implement an application marker interface. Skipping them keeps
    ///     startup scanning proportional to the application rather than to the whole framework surface.
    /// </summary>
    /// <summary>
    ///     Registers every concrete implementation of every interface that extends <typeparamref name="TInterface" />,
    ///     under that sub-interface.
    ///     <para>
    ///         Everything the application references is scanned, whether or not .NET has loaded it yet — see
    ///         <see cref="ApplicationAssemblies" /> for why that distinction matters.
    ///     </para>
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="lifetime">Lifetime used for any implementation the <paramref name="lifetimeSelector" /> does not override.</param>
    /// <param name="registerLazy">
    ///     Also register <c>Lazy&lt;TSubInterface&gt;</c> for each discovered interface, so a consumer can depend on a
    ///     service without constructing it. Registered as transient: a singleton <c>Lazy&lt;T&gt;</c> would capture the
    ///     root provider and hand every scope the same instance of a scoped service.
    /// </param>
    /// <param name="lifetimeSelector">Optional per-implementation lifetime override, e.g. reading an attribute.</param>
    /// <param name="assemblyFilter">
    ///     Optional filter narrowing which loaded assemblies are scanned. Defaults to everything that is not a
    ///     framework assembly. Pass a filter to exclude test doubles from a production container.
    /// </param>
    /// <param name="allowMultipleImplementations">
    ///     Whether one interface may have several implementations. False — the default — throws instead, because
    ///     for a one-interface-one-implementation marker like a use case, a second implementation means
    ///     <c>GetRequiredService&lt;T&gt;</c> silently resolves whichever was registered last. Set true for a
    ///     marker that genuinely describes a catalog resolved as <c>IEnumerable&lt;T&gt;</c>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     An interface extends the marker but has no concrete implementation, or has several while
    ///     <paramref name="allowMultipleImplementations" /> is false.
    /// </exception>
    public static IServiceCollection AddImplementationsOf<TInterface>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        bool registerLazy = false,
        Func<Type, ServiceLifetime?>? lifetimeSelector = null,
        Func<Assembly, bool>? assemblyFilter = null,
        bool allowMultipleImplementations = false)
    {
        var markerType = typeof(TInterface);
        var types = LoadTypes(assemblyFilter);

        // One pass over every type, bucketing implementations by the sub-interface they satisfy.
        var implementationsByInterface = new Dictionary<Type, List<Type>>();
        foreach (var type in types)
        {
            if (!IsConcreteClass(type))
            {
                continue;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                if (interfaceType == markerType || !markerType.IsAssignableFrom(interfaceType))
                {
                    continue;
                }

                if (!implementationsByInterface.TryGetValue(interfaceType, out var implementations))
                {
                    implementations = [];
                    implementationsByInterface[interfaceType] = implementations;
                }

                implementations.Add(type);
            }
        }

        // An interface that extends the marker but resolves to nothing is a wiring mistake that would
        // otherwise surface as a resolution failure on the first request that needs it.
        var declaredInterfaces = types.Where(type => type.IsInterface && markerType.IsAssignableFrom(type) && type != markerType);
        foreach (var declaredInterface in declaredInterfaces)
        {
            if (!implementationsByInterface.ContainsKey(declaredInterface))
            {
                throw new InvalidOperationException(
                    $"No implementation found for {declaredInterface.FullName}, which extends {markerType.Name}. " +
                    "Either implement it or stop extending the marker interface.");
            }
        }

        foreach (var (interfaceType, implementations) in implementationsByInterface)
        {
            if (implementations.Count > 1 && !allowMultipleImplementations)
            {
                var names = string.Join(", ", implementations.Select(type => type.FullName));
                throw new InvalidOperationException(
                    $"{interfaceType.FullName} has {implementations.Count} implementations ({names}), but only one is expected. " +
                    "Resolving it would silently return whichever was registered last. Remove one, register it by hand, " +
                    "or use AddImplementationsOfBase if this interface really is a catalog.");
            }

            foreach (var implementationType in implementations)
            {
                var resolved = lifetimeSelector?.Invoke(implementationType) ?? lifetime;
                services.Add(new ServiceDescriptor(interfaceType, implementationType, resolved));
            }

            if (registerLazy)
            {
                RegisterLazy(services, interfaceType);
            }
        }

        return services;
    }

    /// <summary>
    ///     Registers every concrete class in <paramref name="assembly" /> assignable to <typeparamref name="TBase" />
    ///     under <typeparamref name="TBase" /> itself, so <c>IEnumerable&lt;TBase&gt;</c> resolves them all.
    ///     <para>
    ///         Use this for a closed catalog discovered by its base type — notification definitions, validators,
    ///         startup checks — where missing one silently is worse than registering one too many. Scoped to a single
    ///         assembly on purpose: scanning the whole <see cref="AppDomain" /> would also collect test doubles.
    ///     </para>
    /// </summary>
    public static IServiceCollection AddImplementationsOfBase<TBase>(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        var baseType = typeof(TBase);

        foreach (var type in GetTypesSafely(assembly))
        {
            if (!IsConcreteClass(type) || !baseType.IsAssignableFrom(type))
            {
                continue;
            }

            services.Add(new ServiceDescriptor(baseType, type, lifetime));
        }

        return services;
    }

    private static bool IsConcreteClass(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false };
    }

    private static List<Type> LoadTypes(Func<Assembly, bool>? assemblyFilter)
    {
        return ApplicationAssemblies.All(assemblyFilter).SelectMany(GetTypesSafely).ToList();
    }

    /// <summary>
    ///     An assembly referencing a type it cannot load throws on <see cref="Assembly.GetTypes" /> and would abort
    ///     the whole scan. Keep the types that did load; a broken reference in an unrelated assembly is not this
    ///     registration's problem.
    /// </summary>
    private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null).Cast<Type>();
        }
    }

    private static void RegisterLazy(IServiceCollection services, Type interfaceType)
    {
        var method = typeof(ImplementationRegistrationModule)
            .GetMethod(nameof(RegisterLazyOf), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(interfaceType);

        method.Invoke(null, [services]);
    }

    private static void RegisterLazyOf<T>(IServiceCollection services)
        where T : class
    {
        services.TryAddTransient(provider => new Lazy<T>(provider.GetRequiredService<T>));
    }
}
