using System.Text.Json.Serialization;
using MagicCSharp.Data.KeyGen;
using MagicCSharp.Events;
using MagicCSharp.Extensions;
using MagicCSharp.Infrastructure;
using MagicCSharp.UseCases;
using Microsoft.OpenApi.Models;
using OrderManagement.Business.UseCases.Orders;
using OrderManagement.Data.Domain.Entities;
using OrderManagement.Data.EntityFramework.Repositories;
using OrderManagement.Data.Repositories;
using Revoco.Backend.Modules;

// We need this to load the other assemblies
var _1 = typeof(CreateOrderUseCase).Assembly;
var _2 = typeof(Order).Assembly;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Order Management API", Version = "v1" });
});

// MagicCSharp: Register use cases - automatically finds all [UseCase] classes
// This includes both use cases AND event handlers (they're also use cases!)
builder.Services.AddMagicUseCases();

// MagicCSharp: Register local event dispatcher for development
builder.Services.RegisterMagicEvents();
if (builder.Environment.IsDevelopment())
{
    builder.Services.RegisterLocalMagicEvents();
}
else
{
    // In production, you would use:
    // - builder.Services.RegisterMagicKafkaEvents(kafkaConfig);
    // - builder.Services.RegisterMagicSQSEvents(sqsConfig);
    throw new Exception("Production Environment is not set");
}

// MagicCSharp: Register Snowflake ID generator
// Uses random generator ID for local development
// In production with multiple instances, specify unique IDs per instance:
// builder.Services.RegisterSnowflakeKeyGen(generatorId: instanceId);
builder.Services.RegisterSnowflakeKeyGen();

// MagicCSharp: Register clock abstraction for testable time
builder.Services.AddSingleton<IClock, DateTimeClock>();

// MagicCSharp: Register RequestId handler for distributed tracing
builder.Services.AddSingleton<IRequestIdHandler, RequestIdHandler>();

// Register repositories and the database
builder.Services.AddSQL(builder.Configuration);
builder.Services.AddSingleton<IOrderRepository, OrderEfRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// MagicCSharp: Add RequestId middleware early in the pipeline
// This automatically generates or accepts X-Request-ID headers for distributed tracing
app.UseRequestId();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Order Management API started");
logger.LogInformation("Using MagicCSharp with:");
logger.LogInformation("  - RequestId middleware (X-Request-ID headers)");
logger.LogInformation("  - Local event dispatcher (in-memory)");
logger.LogInformation("  - Snowflake ID generation");
logger.LogInformation("  - Automatic use case registration");
logger.LogInformation("Visit /swagger to see the API documentation");

app.Run();
