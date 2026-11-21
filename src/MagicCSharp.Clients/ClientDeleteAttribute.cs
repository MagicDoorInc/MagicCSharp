namespace MagicCSharp.Clients;

/// <summary>Sends this client method as an HTTP DELETE to the client's route plus <paramref name="template" />.</summary>
/// <param name="template">The path after the client's route; empty for the route itself.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ClientDeleteAttribute(string template = "") : ClientRouteAttribute("DELETE", template);
