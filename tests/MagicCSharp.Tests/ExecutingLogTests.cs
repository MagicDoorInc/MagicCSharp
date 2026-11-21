using System.Reflection;
using MagicCSharp.Modules;
using MagicCSharp.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MagicCSharp.Tests;

/// <summary>
///     A <c>[LogExecuting]</c> use case, built with the real generator and found by use case discovery, writes its
///     <c>Executing:</c> line when called through its interface.
/// </summary>
public class ExecutingLogTests
{
    [Fact]
    public async Task Execute_LogsTheExecutingLineWithItsArguments_WhenResolvedThroughItsInterface()
    {
        // Arrange
        var recordingLoggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(loggingBuilder => loggingBuilder.AddProvider(recordingLoggerProvider).SetMinimumLevel(LogLevel.Trace));
        services.AddMagicUseCases(ThisAssemblyOnly);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var recordPayment = scope.ServiceProvider.GetRequiredService<IRecordPaymentUseCase>();

        // Act
        var receipt = await recordPayment.Execute(41, new RecordPaymentRequest { Reference = "INV-7" });

        // Assert
        Assert.Equal("Payment INV-7 recorded on lease 41", receipt);
        var recordedLogEntry = Assert.Single(recordingLoggerProvider.Entries);
        Assert.Equal("MagicCSharp.Tests.ExecutingLogTests.RecordPaymentUseCase", recordedLogEntry.Category);
        Assert.Equal(LogLevel.Trace, recordedLogEntry.Level);
        Assert.Equal("Executing: leaseId=41, request=RecordPaymentRequest { Reference = INV-7 }", recordedLogEntry.Message);
    }

    private static bool ThisAssemblyOnly(Assembly assembly)
    {
        return assembly == typeof(ExecutingLogTests).Assembly;
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────────

    public interface IRecordPaymentUseCase : IMagicUseCase
    {
        Task<string> Execute(long leaseId, RecordPaymentRequest request);
    }

    public record RecordPaymentRequest
    {
        public required string Reference { get; init; }
    }

    [LogExecuting]
    public class RecordPaymentUseCase : IRecordPaymentUseCase
    {
        public Task<string> Execute(long leaseId, RecordPaymentRequest request)
        {
            return Task.FromResult("Payment " + request.Reference + " recorded on lease " + leaseId);
        }
    }
}
