namespace MagicCSharp.Analyzers.Tests;

public class MethodParameterCountAnalyzerTests
{
    [Fact]
    public async Task A_method_and_a_local_function_with_five_parameters_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new MethodParameterCountAnalyzer(), """
                                                                                            namespace Subject;

                                                                                            public class LeaseCalculator
                                                                                            {
                                                                                                public int Add(int first, int second, int third, int fourth, int fifth)
                                                                                                {
                                                                                                    return Sum(first, second, third, fourth, fifth);

                                                                                                    int Sum(int a, int b, int c, int d, int e)
                                                                                                    {
                                                                                                        return a + b + c + d + e;
                                                                                                    }
                                                                                                }

                                                                                                public int AddFour(int first, int second, int third, int fourth)
                                                                                                {
                                                                                                    return first + second + third + fourth;
                                                                                                }
                                                                                            }
                                                                                            """);

        Assert.Equal([
            "MCS0002 Subject.cs:5 Method 'Add' takes 5 parameters; the limit is 4, so group the related ones into a request type",
            "MCS0002 Subject.cs:9 Method 'Sum' takes 5 parameters; the limit is 4, so group the related ones into a request type",
        ], diagnostics);
    }

    [Fact]
    public async Task A_controller_action_is_exempt_but_a_private_controller_helper_is_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new MethodParameterCountAnalyzer(), """
                                                                                            namespace Subject;

                                                                                            public abstract class MagicControllerBase : Microsoft.AspNetCore.Mvc.ControllerBase
                                                                                            {
                                                                                            }

                                                                                            public class LeasesController : MagicControllerBase
                                                                                            {
                                                                                                public string GetLeases(long companyId, long propertyId, int page, int pageSize, string search)
                                                                                                {
                                                                                                    return Describe(companyId, propertyId, page, pageSize, search);
                                                                                                }

                                                                                                private static string Describe(long companyId, long propertyId, int page, int pageSize, string search)
                                                                                                {
                                                                                                    return search;
                                                                                                }
                                                                                            }
                                                                                            """);

        Assert.Equal(["MCS0002 Subject.cs:14 Method 'Describe' takes 5 parameters; the limit is 4, so group the related ones into a request type"], diagnostics);
    }

    [Fact]
    public async Task Overrides_and_interface_implementations_are_reported_only_where_the_signature_is_declared()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new MethodParameterCountAnalyzer(), """
                                                                                            namespace Subject;

                                                                                            public interface ILeaseWriter
                                                                                            {
                                                                                                void Write(int first, int second, int third, int fourth, int fifth);
                                                                                            }

                                                                                            public class LeaseWriter : ThirdParty.ExternalBase, ILeaseWriter
                                                                                            {
                                                                                                public override bool Enabled => true;

                                                                                                public override void Configure(int first, int second, int third, int fourth, int fifth)
                                                                                                {
                                                                                                }

                                                                                                public void Write(int first, int second, int third, int fourth, int fifth)
                                                                                                {
                                                                                                }
                                                                                            }
                                                                                            """);

        Assert.Equal(["MCS0002 Subject.cs:5 Method 'Write' takes 5 parameters; the limit is 4, so group the related ones into a request type"], diagnostics);
    }

    [Fact]
    public async Task AI_tool_methods_are_exempt()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new MethodParameterCountAnalyzer(), """
                                                                                            namespace Subject;

                                                                                            public class LeaseTools
                                                                                            {
                                                                                                [System.ComponentModel.Description("Searches leases.")]
                                                                                                public string SearchLeases(long companyId, long propertyId, int page, int pageSize, string search)
                                                                                                {
                                                                                                    return search;
                                                                                                }

                                                                                                public string FindLeases(
                                                                                                    [System.ComponentModel.Description("The company.")] long companyId,
                                                                                                    long propertyId,
                                                                                                    int page,
                                                                                                    int pageSize,
                                                                                                    string search)
                                                                                                {
                                                                                                    return search;
                                                                                                }
                                                                                            }
                                                                                            """);

        Assert.Empty(diagnostics);
    }
}
