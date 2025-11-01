using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.UseCases;

/// <summary>
///     Extension methods for registering MagicUseCases in the dependency injection container.
/// </summary>
public static class MagicUseCaseRegistrationExtensions
{
    /// <summary>
    ///     Scans the specified assembly for all interfaces that extend IMagicUseCase
    ///     and automatically registers them with their implementations in the DI container.
    /// </summary>
    /// <param name="services">The service collection to add the use cases to.</param>
    /// <param name="assembly">The assembly to scan for use cases.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMagicUseCases(this IServiceCollection services, Assembly assembly)
    {
        // First, find all interfaces that extend IMagicUseCase (excluding IMagicUseCase itself)
        var useCaseInterfaces = assembly.GetTypes()
            .Where(type => type.IsInterface)
            .Where(type => typeof(IMagicUseCase).IsAssignableFrom(type))
            .Where(type => type != typeof(IMagicUseCase))
            .ToList();

        foreach (var useCaseInterface in useCaseInterfaces)
        {
            // Find the implementation of this interface
            var implementationType = assembly.GetTypes()
                .FirstOrDefault(type => type.IsClass && !type.IsAbstract && useCaseInterface.IsAssignableFrom(type));

            if (implementationType != null)
            {
                var attribute = implementationType.GetCustomAttribute<MagicUseCaseAttribute>();
                var lifetime = attribute?.Lifetime ?? ServiceLifetime.Scoped;

                // Register the interface with its implementation
                services.Add(new ServiceDescriptor(useCaseInterface, implementationType, lifetime));

                // Also register the concrete type
                services.Add(new ServiceDescriptor(implementationType, implementationType, lifetime));
            }
        }

        return services;
    }
}