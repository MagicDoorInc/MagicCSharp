using System.Reflection;
using MagicCSharp.Infrastructure;
using MagicCSharp.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MagicCSharp.Modules;

/// <summary>
///     Extension methods for registering MagicUseCases in the dependency injection container.
/// </summary>
public static class MagicUseCaseRegistrationModule
{
    /// <param name="services">The service collection.</param>
    /// <param name="assemblyFilter">
    ///     Optional filter narrowing which loaded assemblies are scanned for use cases. Defaults to every
    ///     non-framework assembly.
    /// </param>
    public static IServiceCollection AddMagicCSharp(this IServiceCollection services, Func<Assembly, bool>? assemblyFilter = null)
    {
        // .NET's own clock abstraction. TryAdd, so a FakeTimeProvider registered first — in a test host — stays.
        services.TryAddSingleton(TimeProvider.System);

        return services.AddMagicUseCases(assemblyFilter)
            .AddSingleton<IRequestIdHandler, RequestIdHandler>();
    }

    /// <summary>
    ///     Scans the loaded assemblies for every interface that extends <see cref="IMagicUseCase" /> and registers
    ///     each implementation of it. A use case's lifetime comes from its <see cref="MagicUseCaseAttribute" /> when
    ///     it has one, and is Scoped otherwise.
    ///     <para>
    ///         <c>Lazy&lt;IMyUseCase&gt;</c> is registered alongside each interface, so a use case can depend on
    ///         another without constructing it up front — the way out of a construction cycle between two use cases
    ///         that call each other conditionally.
    ///     </para>
    ///     <para>
    ///         Only assemblies already loaded are scanned; see <see cref="ImplementationRegistrationModule.AddImplementationsOf{TInterface}" />
    ///         for why that matters and how to make a project visible.
    ///     </para>
    /// </summary>
    /// <param name="services">The service collection to add the use cases to.</param>
    /// <param name="assemblyFilter">
    ///     Optional filter narrowing which loaded assemblies are scanned. Defaults to every non-framework assembly.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMagicUseCases(this IServiceCollection services, Func<Assembly, bool>? assemblyFilter = null)
    {
        return services.AddImplementationsOf<IMagicUseCase>(new ImplementationRegistrationOptions
        {
            Lifetime = ServiceLifetime.Scoped,
            ShouldRegisterLazy = true,
            LifetimeSelector = GetUseCaseLifetime,
            AssemblyFilter = assemblyFilter,
        });
    }

    private static ServiceLifetime? GetUseCaseLifetime(Type implementationType)
    {
        return implementationType.GetCustomAttribute<MagicUseCaseAttribute>()?.Lifetime;
    }
}
