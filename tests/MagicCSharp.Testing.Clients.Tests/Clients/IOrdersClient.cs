using MagicCSharp.Clients;

namespace MagicCSharp.Testing.Clients.Tests.Clients;

/// <summary>Served by <c>OrdersController</c>, whose route values have other names and constraints.</summary>
[MagicClient("api/orders")]
public interface IOrdersClient
{
    [ClientGet("{orderId}")]
    Task<string?> GetOrder(long orderId);

    [ClientPost]
    Task<string> CreateOrder(string order);

    [ClientPut("{orderId}/lines/{lineId}")]
    Task UpdateLine(long orderId, long lineId);
}
