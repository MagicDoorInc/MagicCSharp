using OrderManagement.Data.Entities;

namespace OrderManagement.Api.DTOs;

public record OrdersResponseDto
{
    public List<OrderResponseDto> Orders { get; init; } = new List<OrderResponseDto>();

    public static OrdersResponseDto FromEntities(IEnumerable<Order> orders)
    {
        return new OrdersResponseDto
        {
            Orders = orders.Select(OrderResponseDto.FromEntity).ToList(),
        };
    }
}