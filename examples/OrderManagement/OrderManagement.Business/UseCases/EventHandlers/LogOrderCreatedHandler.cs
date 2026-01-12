using MagicCSharp.Events.Events;
using MagicCSharp.UseCases;
using Microsoft.Extensions.Logging;
using OrderManagement.Data.Events;

namespace OrderManagement.Business.UseCases.EventHandlers;

// Handler for OrderCreated event - Logs when an order is created
[MagicUseCase]
public class LogOrderCreatedHandler(ILogger<LogOrderCreatedHandler> logger) : IEventHandler<OrderCreated>
{
    public Task Handle(OrderCreated evt)
    {
        // In a real app, this might:
        // - Send to analytics
        // - Update metrics
        // - Trigger notifications
        logger.LogInformation("Order {OrderId} created for user {UserId} with total {Total:C}", evt.OrderId, evt.UserId,
            evt.Total);

        return Task.CompletedTask;
    }
}