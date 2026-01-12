using MagicCSharp.Data.KeyGen;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderManagement.Data.Entities;
using OrderManagement.Data.EntityFramework.DALs;
using OrderManagement.Data.Repositories;

namespace OrderManagement.Data.EntityFramework.Repositories;

public class OrderEfRepository(
    IKeyGenService keyGenService,
    IDbContextFactory<OrderManagementDbContext> contextFactory,
    IClock clock,
    ILogger<OrderEfRepository> logger)
    : BaseIdPaginationRepository<OrderManagementDbContext, OrderDal, Order, OrderFilter, OrderEdit>(contextFactory,
        clock, logger), IOrderRepository
{
    protected override IQueryable<OrderDal> ApplyFilter(IQueryable<OrderDal> query, OrderFilter filter)
    {
        query = query.ApplyNullableValueFilter(filter.OrderId, x => x.Id);
        query = query.ApplyNullableValueFilter(filter.UserId, x => x.UserId);

        return query;
    }

    protected override DbSet<OrderDal> GetDbSet(OrderManagementDbContext context)
    {
        return context.Orders;
    }

    protected override OrderDal CreateDal(OrderEdit edit)
    {
        return OrderDal.From(edit, keyGenService.GetId());
    }

    protected override NotFoundException GetNotFoundException(long id)
    {
        return new NotFoundIdException(id, nameof(Order));
    }

    protected override IQueryable<OrderDal> GetQuery(OrderManagementDbContext context)
    {
        // Eager load OrderItems to avoid N+1 queries
        return context.Orders.Include(x => x.Items).AsSplitQuery();
    }

    protected override void AfterDalCreatedHook(OrderDal dal, OrderEdit edit, OrderManagementDbContext context)
    {
        // For new records: Add all order items
        foreach (var item in edit.Items)
        {
            dal.Items.Add(new OrderItemDal
            {
                Id = keyGenService.GetId(),
                OrderId = dal.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                Price = item.Price,
            });
        }
    }

    protected override void AfterDalApplyHook(OrderDal dal, OrderEdit edit, OrderManagementDbContext context)
    {
        // For updates: Perform add/update/delete logic
        // Track existing items by ProductId for comparison
        var existingByProductId = dal.Items.ToDictionary(x => x.ProductId);
        var processedProductIds = new HashSet<long>();

        // Process items from edit - update existing or add new
        foreach (var itemEdit in edit.Items)
        {
            processedProductIds.Add(itemEdit.ProductId);

            if (existingByProductId.TryGetValue(itemEdit.ProductId, out var existingItem))
            {
                // Update existing item only if values changed
                if (existingItem.ProductName != itemEdit.ProductName || existingItem.Quantity != itemEdit.Quantity ||
                    existingItem.Price != itemEdit.Price)
                {
                    existingItem.ProductName = itemEdit.ProductName;
                    existingItem.Quantity = itemEdit.Quantity;
                    existingItem.Price = itemEdit.Price;
                }
            }
            else
            {
                // Add new item
                dal.Items.Add(new OrderItemDal
                {
                    Id = keyGenService.GetId(),
                    OrderId = dal.Id,
                    ProductId = itemEdit.ProductId,
                    ProductName = itemEdit.ProductName,
                    Quantity = itemEdit.Quantity,
                    Price = itemEdit.Price,
                });
            }
        }

        // Remove items that are no longer in the edit
        var itemsToRemove = dal.Items.Where(x => !processedProductIds.Contains(x.ProductId)).ToList();

        if (itemsToRemove.Any())
        {
            foreach (var item in itemsToRemove)
            {
                dal.Items.Remove(item);
            }
        }
    }
}