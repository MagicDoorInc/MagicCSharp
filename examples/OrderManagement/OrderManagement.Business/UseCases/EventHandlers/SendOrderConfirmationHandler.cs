using MagicCSharp.Events.Events;
using MagicCSharp.UseCases;
using Microsoft.Extensions.Logging;
using OrderManagement.Data.Events;

namespace OrderManagement.Business.UseCases.EventHandlers;

// Handler for OrderConfirmed event - Sends confirmation
[MagicUseCase]
public class SendOrderConfirmationHandler(ILogger<SendOrderConfirmationHandler> logger) : IEventHandler<OrderConfirmed>
{
    public Task Handle(OrderConfirmed evt)
    {
        // In a real app, this would call an email service
        // This is where you'd inject IEmailService
        logger.LogInformation("Sending order confirmation for order {OrderId} to user {UserId}", evt.OrderId,
            evt.UserId);

        return Task.CompletedTask;
    }
}