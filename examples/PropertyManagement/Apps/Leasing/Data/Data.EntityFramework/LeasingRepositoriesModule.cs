using Acme.Leasing.Data.EntityFramework.Repositories;
using Acme.Leasing.Data.Repositories;
using MagicCSharp.Data.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Leasing.Data.EntityFramework;

public static class LeasingRepositoriesModule
{
    /// <summary>
    ///     Registers the context factory and every repository.
    ///     <para>
    ///         Repositories are registered explicitly rather than discovered, so the list of things that talk
    ///         to the database is readable in one place. AddEntity appends to it.
    ///     </para>
    /// </summary>
    public static IServiceCollection AddLeasingRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgresDbContextFactory<MagicLeasingContext>(configuration);
        services.AddScoped<IPropertiesRepository, PropertiesEfRepository>();
        services.AddScoped<ILeasesRepository, LeasesEfRepository>();
        services.AddScoped<INotificationsRepository, NotificationsEfRepository>();
        services.AddScoped<IChargesRepository, ChargesEfRepository>();
        services.AddScoped<ILateFeePoliciesRepository, LateFeePoliciesEfRepository>();

        return services;
    }
}
