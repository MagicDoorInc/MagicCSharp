namespace MagicCSharp.Analyzers.Tests;

public class RequestResultRecordAnalyzerTests
{
    [Fact]
    public async Task Model_classes_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new RequestResultRecordAnalyzer(), """
                                                                                           namespace Subject;

                                                                                           public class ApproveLeaseRequest
                                                                                           {
                                                                                           }

                                                                                           public class ApproveLeaseResult
                                                                                           {
                                                                                           }

                                                                                           public interface IEventHandler<T>
                                                                                           {
                                                                                           }

                                                                                           public class LookalikeOnLeaseCreatedEvent : IEventHandler<ApproveLeaseResult>
                                                                                           {
                                                                                           }
                                                                                           """);

        Assert.Equal([
            "MCS0022 Subject.cs:3 'ApproveLeaseRequest' ends in a model suffix but is a class; declare it as a 'record' instead",
            "MCS0022 Subject.cs:7 'ApproveLeaseResult' ends in a model suffix but is a class; declare it as a 'record' instead",
            "MCS0022 Subject.cs:15 'LookalikeOnLeaseCreatedEvent' ends in a model suffix but is a class; declare it as a 'record' instead",
        ], diagnostics);
    }

    [Fact]
    public async Task Records_event_handlers_derived_abstract_and_static_classes_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new RequestResultRecordAnalyzer(), """
                                                                                           using MagicCSharp.Events.Events;

                                                                                           namespace Subject;

                                                                                           public record ApproveLeaseRequest
                                                                                           {
                                                                                           }

                                                                                           public abstract class MagicEvent
                                                                                           {
                                                                                           }

                                                                                           public class LeaseCreatedEvent : MagicEvent
                                                                                           {
                                                                                           }

                                                                                           public class SendWelcomeEmailOnLeaseCreatedEvent : IEventHandler<LeaseCreatedEvent>
                                                                                           {
                                                                                           }

                                                                                           public abstract class BaseRequest
                                                                                           {
                                                                                           }

                                                                                           public static class ApproveLeasePayload
                                                                                           {
                                                                                           }
                                                                                           """);

        Assert.Empty(diagnostics);
    }
}
