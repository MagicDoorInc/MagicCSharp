namespace MagicCSharp.Testing.Clients;

/// <summary>One controller action as the server routes it.</summary>
public sealed record ServerRoute
{
    public required string Controller { get; init; }

    public required string Action { get; init; }

    /// <summary>The verbs the action accepts; empty when it accepts any.</summary>
    public required IReadOnlyList<string> HttpMethods { get; init; }

    /// <summary>The route template with area, controller and action tokens filled in, e.g. <c>api/orders/{id:long}</c>.</summary>
    public required string Path { get; init; }
}
