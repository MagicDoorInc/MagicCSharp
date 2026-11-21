namespace MagicCSharp.Testing.Clients;

/// <summary>What <see cref="ClientRouteCheck.CheckClientRoutes" /> found on both sides, and the client routes no server route serves.</summary>
public sealed record ClientRouteCheckResult
{
    public required IReadOnlyList<ClientRoute> ClientRoutes { get; init; }

    public required IReadOnlyList<ServerRoute> ServerRoutes { get; init; }

    public required IReadOnlyList<ClientRoute> UnservedClientRoutes { get; init; }
}
