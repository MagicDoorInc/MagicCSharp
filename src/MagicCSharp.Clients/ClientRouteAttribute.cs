namespace MagicCSharp.Clients;

/// <summary>
///     The HTTP method and path template of one client method. The template is relative to the interface's
///     <see cref="MagicClientAttribute.Route" />; <c>{name}</c> in it takes the parameter of that name.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public abstract class ClientRouteAttribute(string httpMethod, string template) : Attribute
{
    /// <summary><c>GET</c>, <c>POST</c>, <c>PUT</c>, <c>PATCH</c> or <c>DELETE</c>.</summary>
    public string HttpMethod { get; } = httpMethod;

    /// <summary>The path after the client's route; empty for the route itself.</summary>
    public string Template { get; } = template;
}
