using Acme.Leasing.Domains.Charges.LateFees.App.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Leasing.Domains.Charges.LateFees.App;

/// <summary>
///     The late-fees background service drives this domain's own use case, so the domain wires it rather than
///     the composition root listing it.
/// </summary>
public static class LateFeesAppModule
{
    public static IServiceCollection AddLateFeesApp(this IServiceCollection services)
    {
        services.AddHostedService<ApplyLateFeesBackgroundService>();

        return services;
    }
}
