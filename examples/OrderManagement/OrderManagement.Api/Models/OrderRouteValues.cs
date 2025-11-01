namespace OrderManagement.Api.Models;

/// <summary>
/// Route values for redirecting to order endpoints
/// </summary>
public class OrderRouteValues
{
    public long OrderId { get; init; }

    public static OrderRouteValues FromOrderId(long orderId)
    {
        return new OrderRouteValues { OrderId = orderId };
    }
}
