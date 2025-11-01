using MagicCSharp.Data.Repositories;
using OrderManagement.Data.Domain.Entities;

namespace OrderManagement.Data.Repositories;

public interface IOrderRepository : IRepositoryPaginated<Order, OrderFilter, OrderEdit>
{
}