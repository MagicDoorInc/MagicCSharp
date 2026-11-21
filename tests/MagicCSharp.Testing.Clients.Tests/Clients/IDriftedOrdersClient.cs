using MagicCSharp.Clients;

namespace MagicCSharp.Testing.Clients.Tests.Clients;

/// <summary>Routes the server stopped serving: a path it never had, and a verb its route does not accept.</summary>
[MagicClient("api/orders")]
public interface IDriftedOrdersClient
{
    [ClientGet("{orderId}/history")]
    Task<string> GetOrderHistory(long orderId);

    [ClientDelete("{orderId}")]
    Task CancelOrder(long orderId);

    /// <summary>Written by hand in a client class; not a route.</summary>
    Task<string> GetOrderSummary(long orderId);
}
