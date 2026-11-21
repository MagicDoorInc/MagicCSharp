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
    ///     Registers every concrete implementation of every interface that extends <typeparamref name="TInterface" />,
    ///     under that sub-interface. See <see cref="AddImplementationsOf(IServiceCollection, Type, ImplementationRegistrationOptions?)" />.
    /// </summary>
    public static IServiceCollection AddImplementationsOf<TInterface>(
        this IServiceCollection services,
        ImplementationRegistrationOptions? options = null)
    {
        return services.AddImplementationsOf(typeof(TInterface), options);
    }

    /// <summary>
    ///     Registers every concrete implementation of every interface that extends <paramref name="markerType" />,
    ///     under that sub-interface.
    ///     <para>
    ///         An open generic marker such as <c>typeof(IRepository&lt;,,,&gt;)</c> matches every closed form of it, and
    ///         registers only non-generic sub-interfaces: <c>IChargesRepository : IRepository&lt;Charge, …&gt;</c> is one,
    ///         a further generic contract such as <c>IPaginatedRepository&lt;,&gt;</c> is not.
    ///     </para>
    ///     <para>
    ///         A class marked <see cref="RegisteredInPlaceOfAttribute" /> takes another's place: it is registered for
    ///         that class's interfaces, and that class as itself, both with that class's lifetime. Without such a
    ///         class, nothing about the registrations changes.
    ///     </para>
    ///     <para>
    ///         Everything the application references is scanned, whether or not .NET has loaded it yet — see
    ///         <see cref="ApplicationAssemblies" /> for why that distinction matters.
    ///     </para>
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="markerType">The interface the registered interfaces extend; closed, or an open generic.</param>
    /// <param name="options">
    ///     Lifetime, lazy registration, assembly filter and whether several implementations are allowed. Omit it for
    ///     transient, one implementation per interface, every application assembly.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     An interface extends the marker but has no concrete implementation, or has several while
    ///     <see cref="ImplementationRegistrationOptions.AllowMultipleImplementations" /> is false.
    /// </exception>
    public static IServiceCollection AddImplementationsOf(
        this IServiceCollection services,
        Type markerType,
        ImplementationRegistrationOptions? options = null)
    {
        options ??= new ImplementationRegistrationOptions();
        var types = LoadTypes(options.AssemblyFilter);

        // One pass over every type, bucketing implementations by the sub-interface they satisfy. A class that takes
        // another's place is set aside, so the interface it shares with that class does not count it twice.
        var implementationsByInterface = new Dictionary<Type, List<Type>>();
        var replacementsByImplementation = new Dictionary<Type, Type>();
        foreach (var type in types)
        {
            if (!IsConcreteClass(type))
            {
                continue;
            }

            var registeredInPlaceOf = type.GetCustomAttribute<RegisteredInPlaceOfAttribute>(false);
            if (registeredInPlaceOf != null)
            {
                replacementsByImplementation[registeredInPlaceOf.ImplementationType] = type;
                continue;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                if (!ExtendsMarker(interfaceType, markerType))
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
        var declaredInterfaces = types.Where(type => type.IsInterface && ExtendsMarker(type, markerType));
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
            if (implementations.Count > 1 && !options.AllowMultipleImplementations)
            {
                var names = string.Join(", ", implementations.Select(type => type.FullName));
                throw new InvalidOperationException(
                    $"{interfaceType.FullName} has {implementations.Count} implementations ({names}), but only one is expected. " +
                    "Resolving it would silently return whichever was registered last. Remove one, register it by hand, " +
                    "or use AddImplementationsOfBase if this interface really is a catalog.");
            }

            foreach (var implementationType in implementations)
            {
                var resolved = options.LifetimeSelector?.Invoke(implementationType) ?? options.Lifetime;
                if (replacementsByImplementation.TryGetValue(implementationType, out var replacementType))
                {
                    AddRegistration(services, new ServiceDescriptor(interfaceType, replacementType, resolved), options);
                    services.TryAdd(new ServiceDescriptor(implementationType, implementationType, resolved));
                    continue;
                }

                AddRegistration(services, new ServiceDescriptor(interfaceType, implementationType, resolved), options);
            }

            if (options.ShouldRegisterLazy)
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

    private static bool ExtendsMarker(Type interfaceType, Type markerType)
    {
        if (!markerType.IsGenericTypeDefinition)
        {
            return interfaceType != markerType && markerType.IsAssignableFrom(interfaceType);
        }

        return !interfaceType.IsGenericType &&
               interfaceType.GetInterfaces().Any(extended => extended.IsGenericType && extended.GetGenericTypeDefinition() == markerType);
    }

    private static void AddRegistration(IServiceCollection services, ServiceDescriptor serviceDescriptor, ImplementationRegistrationOptions options)
    {
        if (options.ShouldKeepExistingRegistrations)
        {
            services.TryAdd(serviceDescriptor);
            return;
        }

        services.Add(serviceDescriptor);
    }

    private static bool IsConcreteClass(Type type)
    {
        return type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters;
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
