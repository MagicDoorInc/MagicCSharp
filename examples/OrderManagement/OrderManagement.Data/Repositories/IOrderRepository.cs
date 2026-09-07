using MagicCSharp.Data.Repositories;
using OrderManagement.Data.Entities;

namespace OrderManagement.Data.Repositories;

public interface IOrderRepository :
    IRepository<Order, long, OrderEdit, OrderFilter>,
    IPaginatedRepository<Order, OrderFilter>
{
}
