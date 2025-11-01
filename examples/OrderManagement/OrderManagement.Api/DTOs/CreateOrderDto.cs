using OrderManagement.Business.UseCases.Orders;

namespace OrderManagement.Api.DTOs;

public record CreateOrderDto
{
    public long UserId { get; init; }
    public List<OrderItemDto> Items { get; init; } = new List<OrderItemDto>();

    public CreateOrderRequest ToRequest()
    {
        return new CreateOrderRequest
        {
            UserId = UserId,
            Items = Items.Select(i => new CreateOrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                Price = i.Price,
            }).ToList(),
        };
    }
}