using IdGen;
using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.Data.KeyGen;

/// <summary>
/// Extension methods for registering key generation services.
/// </summary>
public static class KeyGenServiceExtensions
{
    /// <summary>
    /// Register Snowflake ID generator service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="generatorId">
    /// The generator ID (0-1023). Use different IDs for different application instances
    /// to ensure globally unique IDs across distributed systems. If null, a random ID is generated.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterSnowflakeKeyGen(
        this IServiceCollection services,
        int? generatorId = null)
    {
        var actualGeneratorId = generatorId ?? Random.Shared.Next(1024);

        if (actualGeneratorId < 0 || actualGeneratorId > 1023)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generatorId),
                "Generator ID must be between 0 and 1023");
        }

        // Register IdGenerator as singleton
        services.AddSingleton<IdGenerator>(new IdGenerator(actualGeneratorId));

        // Register IKeyGenService implementation
        services.AddSingleton<IKeyGenService, SnowflakeKeyGenService>();

        return services;
    }
}
