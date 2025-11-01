using OrderManagement.Business.UseCases.Orders;

namespace OrderManagement.Api.DTOs;

public record ProcessCompleteOrderDto
{
    public long UserId { get; init; }
    public List<OrderItemDto> Items { get; init; } = new List<OrderItemDto>();
    public string PaymentMethod { get; init; } = string.Empty;

    public ProcessCompleteOrderRequest ToRequest()
    {
        return new ProcessCompleteOrderRequest
        {
            UserId = UserId,
            Items = Items.Select(i => new CreateOrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                Price = i.Price,
            }).ToList(),
            PaymentMethod = PaymentMethod,
        };
    }
}