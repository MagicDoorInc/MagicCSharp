using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.AspNetCore;

/// <summary>
///     Resolves every registered service once, before the application starts serving.
///     <para>
///         A missing or miswired dependency otherwise surfaces on the first request that happens to need it
///         — which in a service with a rarely-used endpoint can be days after the deploy that broke it, long
///         past the point anyone would connect the two. Resolving everything at startup turns that into a
///         failure the deploy itself reports.
///     </para>
///     <para>
///         Costs one construction of every singleton and scoped service. Worth it at startup; call it only
///         once, after <c>Build()</c>.
///     </para>
/// </summary>
public static class PreflightModule
{
    /// <summary>
    ///     Resolves everything in <paramref name="services" /> from a throwaway scope.
    /// </summary>
    /// <exception cref="InvalidOperationException">Something cannot be constructed.</exception>
    public static IServiceProvider ValidateServices(this IServiceProvider provider, IServiceCollection services)
    {
        var logger = provider.GetService<ILoggerFactory>()?.CreateLogger("MagicCSharp.Preflight");

        using var scope = provider.CreateScope();

        var checkedTypes = 0;
        var failures = new List<string>();

        foreach (var (serviceType, registrations) in Registrations(services))
        {
            try
            {
                if (registrations > 1)
                {
                    // Enumerate, so every registration behind IEnumerable<T> is actually constructed.
                    foreach (var _ in scope.ServiceProvider.GetServices(serviceType))
                    {
                    }
                }
                else
                {
                    _ = scope.ServiceProvider.GetRequiredService(serviceType);
                }

                checkedTypes++;
            }
            catch (Exception ex)
            {
                // Collect rather than throw on the first, so one run reports every broken registration
                // instead of one per deploy.
                failures.Add($"{serviceType.FullName}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"{failures.Count} service(s) could not be resolved:{Environment.NewLine}  " +
                string.Join(Environment.NewLine + "  ", failures));
        }

        logger?.LogInformation("Preflight: {Count} service types resolved", checkedTypes);

        return provider;
    }

    /// <summary>
    ///     Service types worth resolving, with how many times each is registered. Open generics are skipped:
    ///     there is nothing to resolve until something closes them.
    /// </summary>
    private static IEnumerable<(Type ServiceType, int Registrations)> Registrations(IServiceCollection services)
    {
        var counts = new Dictionary<Type, int>();

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType.IsGenericTypeDefinition || descriptor.ServiceType.ContainsGenericParameters)
            {
                continue;
            }

            // A keyed service cannot be resolved without its key, and guessing keys is not this method's
            // job. ASP.NET registers several of its own that way — AddOpenApi's document services, for one
            // — so treating them as failures would stop an ordinary application booting.
            if (descriptor.IsKeyedService)
            {
                continue;
            }

            counts[descriptor.ServiceType] = counts.GetValueOrDefault(descriptor.ServiceType) + 1;
        }

        return counts.Select(pair => (pair.Key, pair.Value));
    }
}
