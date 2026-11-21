namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     The verbs that name a read-only use case for the <c>Executing:</c> line: a use case named with one of them
///     never writes the line, so <c>[LogExecuting]</c> on it is refused.
/// </summary>
public static class ReadOnlyUseCaseVerbs
{
    public static IReadOnlyList<string> All { get; } = new[] { "Get", "List", "Search", "Find", "Is", "Can", "Count" };
}
