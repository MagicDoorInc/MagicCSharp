using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MagicCSharp.Data.EntityFramework;

/// <summary>
///     Registers a context's repositories by convention instead of a line per repository.
/// </summary>
public static class RepositoryRegistrationModule
{
    /// <summary>
    ///     Registers every repository in <typeparamref name="TContext" />'s assembly, and
    ///     <see cref="RepositoryServices{TContext}" /> for their constructors.
    ///     <para>
    ///         A repository is a concrete class implementing a non-generic interface that extends
    ///         <see cref="IRepository{TEntity,TKey,TEdit,TFilter}" />; it is registered under each such interface, so
    ///         its class name does not matter. An interface already registered by hand keeps that registration. Two
    ///         classes implementing one such interface throw, as <see cref="ImplementationRegistrationModule" /> does.
    ///     </para>
    ///     <para>The context factory itself is registered by the provider package, not here.</para>
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="lifetime">The lifetime of the repositories. Scoped unless given.</param>
    public static IServiceCollection AddRepositoriesFrom<TContext>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        var repositoryAssembly = typeof(TContext).Assembly;

        services.TryAddTransient<RepositoryServices<TContext>>();
        services.AddImplementationsOf(typeof(IRepository<,,,>), new ImplementationRegistrationOptions
        {
            Lifetime = lifetime,
            AssemblyFilter = assembly => assembly == repositoryAssembly,
            ShouldKeepExistingRegistrations = true,
        });

        return services;
    }
}
