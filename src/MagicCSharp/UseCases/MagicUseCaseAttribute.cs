using Microsoft.Extensions.DependencyInjection;

namespace MagicCSharp.UseCases;

/// <summary>
///     Attribute to mark a class as a MagicUseCase and specify its dependency injection lifetime.
/// </summary>
/// <param name="lifetime">The service lifetime for the use case. Defaults to Scoped.</param>
[AttributeUsage(AttributeTargets.Class)]
public class MagicUseCaseAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped) : Attribute
{
    /// <summary>
    ///     Gets or sets the service lifetime for the use case. Defaults to Scoped.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = lifetime;
}