namespace MagicCSharp.Analyzers.Tests;

public class ReadOnlyUseCaseLoggerAnalyzerTests
{
    private const string LoggerStub = """
                                      namespace Subject
                                      {
                                          public interface ITraceLogger
                                          {
                                              void LogTrace(string message, params object[] args);

                                              void LogWarning(string message, params object[] args);
                                          }
                                      }
                                      """;

    [Fact]
    public async Task The_Executing_line_in_read_only_use_cases_is_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new ReadOnlyUseCaseLoggerAnalyzer(), LoggerStub + """

            namespace Subject
            {
                public class GetLeaseUseCase(ITraceLogger logger) : MagicCSharp.UseCases.IMagicUseCase
                {
                    public void Execute(long leaseId)
                    {
                        logger.LogTrace("Executing: leaseId={leaseId}", leaseId);
                    }
                }

                public class ExternalListLeasesUseCase(ITraceLogger logger) : MagicCSharp.UseCases.IMagicUseCase
                {
                    public void Execute(long companyId)
                    {
                        logger.LogTrace($"Executing: companyId={companyId}");
                    }
                }
            }
            """);

        Assert.Equal([
            "MCS0015 Subject.cs:16 'GetLeaseUseCase' is a read-only use case; remove the \"Executing:\" log line",
            "MCS0015 Subject.cs:24 'ExternalListLeasesUseCase' is a read-only use case; remove the \"Executing:\" log line",
        ], diagnostics);
    }

    [Fact]
    public async Task Other_logging_and_side_effect_use_cases_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new ReadOnlyUseCaseLoggerAnalyzer(), LoggerStub + """

            namespace Subject
            {
                public class GetLeaseUseCase(ITraceLogger logger) : MagicCSharp.UseCases.IMagicUseCase
                {
                    public void Execute(long leaseId)
                    {
                        logger.LogWarning("Lease {leaseId} belongs to another company", leaseId);
                    }
                }

                public class CreateLeaseUseCase(ITraceLogger logger) : MagicCSharp.UseCases.IMagicUseCase
                {
                    public void Execute(long leaseId)
                    {
                        logger.LogTrace("Executing: leaseId={leaseId}", leaseId);
                    }
                }

                public class GetterSetupUseCase(ITraceLogger logger) : MagicCSharp.UseCases.IMagicUseCase
                {
                    public void Execute(long leaseId)
                    {
                        logger.LogTrace("Executing: leaseId={leaseId}", leaseId);
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }
}
