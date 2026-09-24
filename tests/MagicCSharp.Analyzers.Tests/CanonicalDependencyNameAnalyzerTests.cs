namespace MagicCSharp.Analyzers.Tests;

public class CanonicalDependencyNameAnalyzerTests
{
    [Fact]
    public async Task Infrastructure_dependencies_with_other_names_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new CanonicalDependencyNameAnalyzer(), """
                                                                                               using MagicCSharp.Events.Events;
                                                                                               using MagicCSharp.Infrastructure.KeyGen;

                                                                                               namespace Subject;

                                                                                               public class LeaseService(
                                                                                                   IKeyGenService keyGen,
                                                                                                   System.TimeProvider time,
                                                                                                   IEventDispatcher dispatcher,
                                                                                                   Medallion.Threading.IDistributedLockProvider locks,
                                                                                                   Microsoft.Extensions.Logging.ILogger<LeaseService> log)
                                                                                               {
                                                                                               }
                                                                                               """);

        Assert.Equal([
            "MCS0020 Subject.cs:7 Dependency of type 'IKeyGenService' is named 'keyGen'; name it 'keyGenService'",
            "MCS0020 Subject.cs:8 Dependency of type 'TimeProvider' is named 'time'; name it 'timeProvider'",
            "MCS0020 Subject.cs:9 Dependency of type 'IEventDispatcher' is named 'dispatcher'; name it 'eventDispatcher'",
            "MCS0020 Subject.cs:10 Dependency of type 'IDistributedLockProvider' is named 'locks'; name it 'distributedLockProvider'",
            "MCS0020 Subject.cs:11 Dependency of type 'ILogger' is named 'log'; name it 'logger'",
        ], diagnostics);
    }

    [Fact]
    public async Task Canonical_names_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new CanonicalDependencyNameAnalyzer(), """
                                                                                               using MagicCSharp.Events.Events;
                                                                                               using MagicCSharp.Infrastructure.KeyGen;

                                                                                               namespace Subject;

                                                                                               public interface IKeyGenService
                                                                                               {
                                                                                               }

                                                                                               public class LeaseService(
                                                                                                   MagicCSharp.Infrastructure.KeyGen.IKeyGenService keyGenService,
                                                                                                   System.TimeProvider timeProvider,
                                                                                                   IEventDispatcher eventDispatcher,
                                                                                                   Medallion.Threading.IDistributedLockProvider distributedLockProvider,
                                                                                                   Microsoft.Extensions.Logging.ILogger logger,
                                                                                                   Subject.IKeyGenService keys)
                                                                                               {
                                                                                               }
                                                                                               """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task FromServices_infrastructure_dependencies_with_other_names_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new CanonicalDependencyNameAnalyzer(), """
                                                                                               using MagicCSharp.Infrastructure.KeyGen;
                                                                                               using Microsoft.AspNetCore.Mvc;

                                                                                               namespace Subject;

                                                                                               public class LeasesController : ControllerBase
                                                                                               {
                                                                                                   public void Create([FromServices] System.TimeProvider time, [FromServices] IKeyGenService keyGenService)
                                                                                                   {
                                                                                                   }
                                                                                               }
                                                                                               """);

        Assert.Equal([
            "MCS0020 Subject.cs:8 Dependency of type 'TimeProvider' is named 'time'; name it 'timeProvider'",
        ], diagnostics);
    }
}
