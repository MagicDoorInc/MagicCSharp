using MagicCSharp.Events.Events;
using MagicCSharp.Infrastructure;
using MagicCSharp.UseCases;
using OrderManagement.Data.Entities;
using OrderManagement.Data.Events;
using OrderManagement.Data.Repositories;

namespace OrderManagement.Business.UseCases.Orders;

// Request - Input data
public record CreateOrderRequest
{
    public long UserId { get; init; }
    public List<CreateOrderItem> Items { get; init; } = new List<CreateOrderItem>();
}

public record CreateOrderItem
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Price { get; init; }
}

// Result - Output data
public record CreateOrderResult
{
    public long OrderId { get; init; }
    public decimal Total { get; init; }
    public OrderStatus Status { get; init; }
}

// Interface
public interface ICreateOrderUseCase : IMagicUseCase
{
    Task<CreateOrderResult> Execute(CreateOrderRequest request);
}

// Implementation - Pure business logic
[MagicUseCase]
public class CreateOrderUseCase(
    IOrderRepository orderRepository,
    IEventDispatcher events,
    IClock clock) : ICreateOrderUseCase
{
    public async Task<CreateOrderResult> Execute(CreateOrderRequest request)
    {
        // Build order items
        var items = request.Items
            .Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                Price = i.Price,
            })
            .ToList();

        // Calculate total
        var total = items.Sum(i => i.Subtotal);

        // Create using repository pattern - OrderEdit separates the "how to create" from the entity
        var order = await orderRepository.Create(new OrderEdit
        {
            UserId = request.UserId,
            Items = items,
            Total = total,
            Status = OrderStatus.Pending,
        });

        // Dispatch event - event handlers will process async
        events.Dispatch(new OrderCreated
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Total = order.Total,
            CreatedAt = clock.Now().DateTime,
        });

        return new CreateOrderResult
        {
            OrderId = order.Id,
            Total = order.Total,
            Status = order.Status,
        };
    }
}