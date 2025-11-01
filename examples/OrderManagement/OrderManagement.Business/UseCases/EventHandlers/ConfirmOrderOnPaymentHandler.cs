using MagicCSharp.Events;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Exceptions;
using MagicCSharp.UseCases;
using Microsoft.Extensions.Logging;
using OrderManagement.Data.Domain.Entities;
using OrderManagement.Data.Domain.Events;
using OrderManagement.Data.Repositories;

namespace OrderManagement.Business.UseCases.EventHandlers;

// Handler for PaymentProcessed event - Confirms the order
[MagicUseCase]
public class ConfirmOrderOnPaymentHandler(
    IOrderRepository orderRepository,
    IEventDispatcher events,
    IClock clock,
    ILogger<ConfirmOrderOnPaymentHandler> logger) : IEventHandler<PaymentProcessed>
{
    public async Task Handle(PaymentProcessed evt)
    {
        logger.LogInformation("Payment processed for order {OrderId}, confirming order", evt.OrderId);

        // Get the order using repository pattern
        var order = await orderRepository.Get(evt.OrderId);
        NotFoundException.ThrowIfNull(order, evt.OrderId);

        // Update order status
        order = order with
        {
            Status = OrderStatus.Confirmed,
            CompletedAt = clock.Now().DateTime,
        };
        await orderRepository.Update(order);

        // Dispatch another event - events can trigger more events!
        events.Dispatch(new OrderConfirmed
        {
            OrderId = order.Id,
            UserId = order.UserId,
            ConfirmedAt = order.CompletedAt.Value,
        });
    }
}