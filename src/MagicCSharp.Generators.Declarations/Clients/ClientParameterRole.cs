namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Where a client method's parameter goes in the request.</summary>
internal enum ClientParameterRole
{
    /// <summary>A <c>{name}</c> in the route.</summary>
    RouteValue,

    /// <summary>A simple value in the query string, gathered with the method's other simple values.</summary>
    QueryValue,

    /// <summary>An object on a GET whose properties go to the query string.</summary>
    QueryObject,

    /// <summary>The one object of a write, sent as the JSON body.</summary>
    Body,

    /// <summary>Passed on to the send.</summary>
    CancellationToken,
}
