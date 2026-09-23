namespace MagicCSharp.Analyzers.Tests;

public class PredicateBooleanNameAnalyzerTests
{
    [Fact]
    public async Task Booleans_that_do_not_read_as_questions_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new PredicateBooleanNameAnalyzer(), """
                                                                                            namespace Subject;

                                                                                            public class LeaseFlags
                                                                                            {
                                                                                                private bool closed;

                                                                                                public bool Active { get; init; }

                                                                                                public void Load(bool includeDeleted)
                                                                                                {
                                                                                                    var enabled = includeDeleted && Active && closed;
                                                                                                }
                                                                                            }
                                                                                            """);

        Assert.Equal([
            "MCS0011 Subject.cs:5 Boolean 'closed' does not start with is/has/can/should/allow/was/will/must/are; rename it so it reads as a yes/no question",
            "MCS0011 Subject.cs:7 Boolean 'Active' does not start with is/has/can/should/allow/was/will/must/are; rename it so it reads as a yes/no question",
            "MCS0011 Subject.cs:9 Boolean 'includeDeleted' does not start with is/has/can/should/allow/was/will/must/are; rename it so it reads as a yes/no question",
            "MCS0011 Subject.cs:11 Boolean 'enabled' does not start with is/has/can/should/allow/was/will/must/are; rename it so it reads as a yes/no question",
        ], diagnostics);
    }

    [Fact]
    public async Task Obsolete_contract_properties_overrides_lambdas_AI_tool_parameters_and_nullable_booleans_are_skipped()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new PredicateBooleanNameAnalyzer(), """
                                                                                            using System.Linq;

                                                                                            namespace Subject;

                                                                                            public record LeaseDto
                                                                                            {
                                                                                                public bool IsClosed { get; init; }

                                                                                                [System.Obsolete("Use IsClosed")]
                                                                                                public bool Closed
                                                                                                {
                                                                                                    get => IsClosed;
                                                                                                    init => IsClosed = value;
                                                                                                }

                                                                                                public bool? HasDeposit { get; init; }

                                                                                                public bool? Renewed { get; init; }
                                                                                            }

                                                                                            public class LeaseSettings : ThirdParty.ExternalBase
                                                                                            {
                                                                                                public override bool Enabled => true;

                                                                                                public override void Configure(int first, int second, int third, int fourth, int fifth)
                                                                                                {
                                                                                                    var canConfigure = new[] { true }.Any(flag => flag);
                                                                                                }

                                                                                                [System.ComponentModel.Description("Sends a chat message.")]
                                                                                                public string SendChatMessage(string message, bool send_as_text)
                                                                                                {
                                                                                                    return message;
                                                                                                }
                                                                                            }
                                                                                            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Parameters_named_by_an_overridden_or_implemented_contract_are_skipped()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new PredicateBooleanNameAnalyzer(), """
            namespace Subject;

            public abstract class ContextBase
            {
                public abstract int SaveChanges(bool isAcceptingAllChanges);
            }

            public interface ISaver
            {
                void Save(bool isForced);
            }

            public class Context : ContextBase, ISaver
            {
                public override int SaveChanges(bool acceptAllChangesOnSuccess)
                {
                    return 0;
                }

                public void Save(bool force)
                {
                }
            }
            """);

        Assert.Empty(diagnostics);
    }
}
