using MagicCSharp.Cli.Commands;

namespace MagicCSharp.Cli.Tests;

public class EventValidationTests
{
    [Fact]
    public void An_entity_on_an_event_is_reported()
    {
        var violations = Validate("""
                                  public record OrderPlacedEvent : MagicEvent
                                  {
                                      public required Order Order { get; init; }
                                  }
                                  """);

        var violation = Assert.Single(violations);
        Assert.Equal("Order is Order", violation.Message);
    }

    [Fact]
    public void Ids_and_primitives_on_an_event_are_accepted()
    {
        var violations = Validate("""
                                  public record OrderPlacedEvent : MagicEvent
                                  {
                                      public required long OrderId { get; init; }
                                      public required List<long> LineIds { get; init; }
                                  }
                                  """);

        Assert.Empty(violations);
    }

    [Fact]
    public void A_type_that_is_not_an_event_is_ignored_even_beside_one()
    {
        var violations = Validate("""
                                  public record OrderPlacedEvent : MagicEvent
                                  {
                                      public required long OrderId { get; init; }
                                  }

                                  public record OrderSummary
                                  {
                                      public required Order Order { get; init; }
                                  }
                                  """);

        Assert.Empty(violations);
    }

    private static List<Violation> Validate(string source)
    {
        var sourceFile = new SourceFile { Path = "OrderPlacedEvent.cs", Lines = source.Split('\n') };
        return ValidateCommand.EventPropertiesArePrimitive([sourceFile]).ToList();
    }
}
