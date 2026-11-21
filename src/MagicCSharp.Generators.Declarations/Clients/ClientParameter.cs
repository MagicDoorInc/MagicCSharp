namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>One parameter of a client method and the C# that puts it in the request.</summary>
internal sealed record ClientParameter
{
    /// <summary>The parameter's name as C# writes it (escaped when it is a keyword).</summary>
    public required string Name { get; init; }

    public required ClientParameterRole Role { get; init; }

    /// <summary>For a route value, the expression that writes it into the path; null otherwise.</summary>
    public required string? RouteExpression { get; init; }
}
