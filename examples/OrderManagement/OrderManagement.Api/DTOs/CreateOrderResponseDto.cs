using OrderManagement.Business.UseCases.Orders;
using OrderManagement.Data.Domain.Entities;

namespace OrderManagement.Api.DTOs;

public record CreateOrderResponseDto
{
    public long OrderId { get; init; }
    public decimal Total { get; init; }
    public OrderStatus Status { get; init; }

    public static CreateOrderResponseDto FromResult(CreateOrderResult result)
    {
        return new CreateOrderResponseDto
        {
            OrderId = result.OrderId,
            Total = result.Total,
            Status = result.Status,
        };
    }
}
