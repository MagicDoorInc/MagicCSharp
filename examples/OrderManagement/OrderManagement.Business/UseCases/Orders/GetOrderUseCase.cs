using MagicCSharp.Infrastructure.Exceptions;
using MagicCSharp.UseCases;
using OrderManagement.Data.Entities;
using OrderManagement.Data.Repositories;

namespace OrderManagement.Business.UseCases.Orders;

// Interface
public interface IGetOrderUseCase : IMagicUseCase
{
    Task<Order> Execute(long orderId);
}

// Implementation
[MagicUseCase]
public class GetOrderUseCase(IOrderRepository orderRepository) : IGetOrderUseCase
{
    public async Task<Order> Execute(long orderId)
    {
        var order = await orderRepository.Get(orderId);
        NotFoundException.ThrowIfNull(order, orderId);

        return order;
    }
}