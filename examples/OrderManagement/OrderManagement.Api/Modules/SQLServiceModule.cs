using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OrderManagement.Data.EntityFramework;
using MagicCSharp.Infrastructure;

namespace Revoco.Backend.Modules;

public static class SQLServiceModule
{
    public static IServiceCollection AddSQL(this IServiceCollection services, IConfiguration configurationManager)
    {
        // Use AWS Secrets Manager for dev/prod environments
        // Connection details come from configuration, only password from Secrets Manager
        var host = configurationManager.GetValue<string>("DB_HOST") ?? throw new Exception("Missing DB_HOST");
        var port = configurationManager.GetValue<string>("DB_PORT") ?? throw new Exception("Missing DB_PORT");
        var database = configurationManager.GetValue<string>("DB_NAME") ?? throw new Exception("Missing DB_NAME");
        var user = configurationManager.GetValue<string>("DB_USER") ?? throw new Exception("Missing DB_USER");
        var password = configurationManager.GetValue<string>("DB_PASSWORD") ??
                   throw new Exception("Missing DB_PASSWORD");

        var connectionString = $"Host={host};" + $"Port={port};" + $"Database={database};" + $"Username={user};" +
                               $"Password={password};" + $"Timeout=60;" + // 60 seconds
                               $"Command Timeout=180;" + // 180 seconds
                               $"Maximum Pool Size=500;" + // server limit is 5000, we have 4 nodes
                               $"Minimum Pool Size=50;" + //
                               $"Connection Idle Lifetime=600;" + // 10 minutes
                               $"Keepalive=60;" + // 60 seconds
                               $"Tcp Keepalive=true;" + $"Include Error Detail=true;" + $"Log Parameters=true;";

        services.AddPooledDbContextFactory<OrderManagementDbContext>(options =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson()
                .ConfigureJsonOptions(JsonDefaults.Options);

            var dataSource = dataSourceBuilder.Build();
            options.UseNpgsql(dataSource, o => o.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null));
            options.EnableSensitiveDataLogging();
            options.ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
        });

        return services;
    }
}