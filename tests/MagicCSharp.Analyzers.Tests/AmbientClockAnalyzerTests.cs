namespace MagicCSharp.Analyzers.Tests;

public class AmbientClockAnalyzerTests
{
    [Fact]
    public async Task System_clock_reads_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new AmbientClockAnalyzer(), """
                                                                                    namespace Subject;

                                                                                    public class LeaseDates
                                                                                    {
                                                                                        public System.DateTime Today()
                                                                                        {
                                                                                            return System.DateTime.UtcNow;
                                                                                        }

                                                                                        public System.DateTimeOffset Now()
                                                                                        {
                                                                                            return System.DateTimeOffset.Now;
                                                                                        }

                                                                                        public System.DateTime Midnight()
                                                                                        {
                                                                                            return System.DateTime.Today;
                                                                                        }
                                                                                    }
                                                                                    """);

        Assert.Equal([
            "MCS0008 Subject.cs:7 Replace 'DateTime.UtcNow' with an injected TimeProvider and call 'timeProvider.GetUtcNow()'",
            "MCS0008 Subject.cs:12 Replace 'DateTimeOffset.Now' with an injected TimeProvider and call 'timeProvider.GetUtcNow()'",
            "MCS0008 Subject.cs:17 Replace 'DateTime.Today' with an injected TimeProvider and call 'timeProvider.GetUtcNow()'",
        ], diagnostics);
    }

    [Fact]
    public async Task Only_a_class_deriving_from_System_TimeProvider_may_read_the_system_clock()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new AmbientClockAnalyzer(), """
                                                                                    namespace Subject;

                                                                                    public abstract class TimeProvider
                                                                                    {
                                                                                        public abstract System.DateTimeOffset GetUtcNow();
                                                                                    }

                                                                                    public class WallClockTimeProvider : System.TimeProvider
                                                                                    {
                                                                                        public override System.DateTimeOffset GetUtcNow()
                                                                                        {
                                                                                            return global::System.DateTimeOffset.UtcNow;
                                                                                        }
                                                                                    }

                                                                                    public class LookalikeTimeProvider : TimeProvider
                                                                                    {
                                                                                        public override System.DateTimeOffset GetUtcNow()
                                                                                        {
                                                                                            return System.DateTimeOffset.UtcNow;
                                                                                        }
                                                                                    }

                                                                                    public class LeaseDates(System.TimeProvider timeProvider)
                                                                                    {
                                                                                        public System.DateTimeOffset Now()
                                                                                        {
                                                                                            return timeProvider.GetUtcNow();
                                                                                        }
                                                                                    }
                                                                                    """);

        Assert.Equal(["MCS0008 Subject.cs:20 Replace 'DateTimeOffset.UtcNow' with an injected TimeProvider and call 'timeProvider.GetUtcNow()'"], diagnostics);
    }
}
