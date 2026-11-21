namespace MagicCSharp.Clients;

/// <summary>
///     Marks an interface as the client of another service's HTTP API. <c>MagicCSharp.Generators</c> writes its
///     client class (<c>IOrdersClient</c> → <c>OrdersClient</c>), with one method per member that carries a client
///     route attribute, and <c>Add{ClientClass}</c> to register it. Members without a route attribute are written
///     by hand in <c>partial class {ClientClass}</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class MagicClientAttribute(string route) : Attribute
{
    /// <summary>The path every method's template is relative to, e.g. <c>api/orders</c>.</summary>
    public string Route { get; } = route;

    /// <summary>The configuration key holding the service's base URL, e.g. <c>Services:Orders:Url</c>.</summary>
    public string? UrlSetting { get; set; }

    /// <summary>The base URL used when the configuration has no value under <see cref="UrlSetting" />.</summary>
    public string? DefaultUrl { get; set; }
}
