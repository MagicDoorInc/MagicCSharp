using OrderManagement.Data.Entities;

namespace OrderManagement.Api.DTOs;

public record OrderResponseDto
{
    public long OrderId { get; init; }
    public long UserId { get; init; }
    public List<OrderItemDto> Items { get; init; } = new List<OrderItemDto>();
    public decimal Total { get; init; }
    public OrderStatus Status { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }

    public static OrderResponseDto FromEntity(Order order)
    {
        return new OrderResponseDto
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Items = order.Items
                .Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    Price = i.Price,
                })
                .ToList(),
            Total = order.Total,
            Status = order.Status,
            Created = order.Created,
            CompletedAt = order.CompletedAt,
        };
    }
}