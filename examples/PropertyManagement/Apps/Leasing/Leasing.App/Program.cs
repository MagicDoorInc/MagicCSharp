using Acme.Leasing.Data.EntityFramework;
using Acme.Leasing.Domains.Charges.LateFees.App;
using MagicCSharp.App;
using MagicCSharp.Events.Kafka;

namespace Acme.Leasing.App;

/// <summary>
///     Named rather than top-level so integration tests can reference it as
///     <c>WebApplicationFactory&lt;LeasingProgram&gt;</c>.
/// </summary>
public class LeasingProgram
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Events go through Kafka, which docker-compose.yml runs. Registered before AddMagicApp, so the
        // in-process dispatcher steps aside; no publisher or handler knows which transport it is on.
        var kafkaConfiguration = builder.Configuration.GetRequiredSection("Kafka").Get<KafkaMagicEventConfiguration>()!;
        builder.Services.AddMagicKafkaEvents(kafkaConfiguration);

        // Use cases, TimeProvider, Snowflake IDs, request IDs, in-process events, scheduling defaults and
        // problem-details error handling. Pass MagicAppOptions to change any of it; swap in
        // AddMagicKafkaEvents before this line and the in-process dispatcher steps aside.
        builder.AddMagicApp();

        builder.Services.AddOpenApi();

        builder.Services.AddLeasingRepositories(builder.Configuration);
        builder.Services.AddLateFeesApp();

        var app = builder.Build();

        // Request IDs, error handling and controllers, in the order they need — and a preflight that
        // resolves every registration now rather than on the first request that needs it.
        app.UseMagicApp(builder);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.Run();
    }
}
