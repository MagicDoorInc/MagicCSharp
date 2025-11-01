using MagicCSharp.UseCases;
using OrderManagement.Data.Domain.Entities;
using OrderManagement.Data.Repositories;

namespace OrderManagement.Business.UseCases.Orders;

// Interface
public interface IGetUserOrdersUseCase : IMagicUseCase
{
    Task<IReadOnlyList<Order>> Execute(long userId);
}

// Implementation
[MagicUseCase]
public class GetUserOrdersUseCase(IOrderRepository orderRepository) : IGetUserOrdersUseCase
{
    public async Task<IReadOnlyList<Order>> Execute(long userId)
    {
        var orders = await orderRepository.Get(new OrderFilter
        {
            UserId = userId,
        });

        return orders;
    }
}