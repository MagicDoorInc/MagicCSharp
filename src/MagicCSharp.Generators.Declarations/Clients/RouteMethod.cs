using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>A route-attributed interface method, before it is read: what the attribute says about it.</summary>
internal sealed record RouteMethod
{
    public required IMethodSymbol Method { get; init; }

    public required ClientHttpMethod HttpMethod { get; init; }

    /// <summary>The client's route and the method's template, trimmed of slashes.</summary>
    public required string Path { get; init; }
}
