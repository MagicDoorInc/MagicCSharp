namespace MagicCSharp.Testing.Clients;

/// <summary>One method of a <c>[MagicClient]</c> interface: the verb and path it sends.</summary>
public sealed record ClientRoute
{
    /// <summary>The client interface's name, e.g. <c>IOrdersClient</c>.</summary>
    public required string Client { get; init; }

    public required string Method { get; init; }

    public required string HttpMethod { get; init; }

    /// <summary>The client's route and the method's template, e.g. <c>api/orders/{orderId}</c>.</summary>
    public required string Path { get; init; }
}
