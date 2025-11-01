using MagicCSharp.Events;
using MagicCSharp.Infrastructure;
using MagicCSharp.Infrastructure.Exceptions;
using MagicCSharp.UseCases;
using OrderManagement.Data.Domain.Entities;
using OrderManagement.Data.Domain.Events;
using OrderManagement.Data.Repositories;

namespace OrderManagement.Business.UseCases.Orders;

// Request
public record ProcessPaymentRequest
{
    public long OrderId { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
}

// Result
public record ProcessPaymentResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

// Interface
public interface IProcessPaymentUseCase : IMagicUseCase
{
    Task<ProcessPaymentResult> Execute(ProcessPaymentRequest request);
}

// Implementation
[MagicUseCase]
public class ProcessPaymentUseCase(
    IOrderRepository orderRepository,
    IEventDispatcher events,
    IClock clock) : IProcessPaymentUseCase
{
    public async Task<ProcessPaymentResult> Execute(ProcessPaymentRequest request)
    {
        // Get the order using repository pattern
        var order = await orderRepository.Get(request.OrderId);
        NotFoundException.ThrowIfNull(order, request.OrderId);

        // Business rule: Can only process payment for pending orders
        if (order.Status != OrderStatus.Pending)
        {
            return new ProcessPaymentResult
            {
                Success = false,
                Message = $"Cannot process payment for order in {order.Status} status",
            };
        }

        // Update order status using repository Update method
        order = order with
        {
            Status = OrderStatus.PaymentProcessing,
        };
        await orderRepository.Update(order);

        // Simulate payment processing (in real app, call payment service)
        // This would be a Service layer component
        await Task.Delay(100);

        // Dispatch payment processed event
        events.Dispatch(new PaymentProcessed
        {
            OrderId = order.Id,
            Amount = order.Total,
            PaymentMethod = request.PaymentMethod,
            ProcessedAt = clock.Now(),
        });

        return new ProcessPaymentResult
        {
            Success = true,
            Message = "Payment processed successfully",
        };
    }
}