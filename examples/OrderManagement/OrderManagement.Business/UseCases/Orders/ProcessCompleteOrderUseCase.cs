using MagicCSharp.UseCases;

namespace OrderManagement.Business.UseCases.Orders;

// Request
public record ProcessCompleteOrderRequest
{
    public long UserId { get; init; }
    public List<CreateOrderItem> Items { get; init; } = new List<CreateOrderItem>();
    public string PaymentMethod { get; init; } = string.Empty;
}

// Result
public record ProcessCompleteOrderResult
{
    public long OrderId { get; init; }
    public decimal Total { get; init; }
    public bool PaymentSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
}

// Interface
public interface IProcessCompleteOrderUseCase : IMagicUseCase
{
    Task<ProcessCompleteOrderResult> Execute(ProcessCompleteOrderRequest request);
}

// Implementation - Demonstrates use case chaining
[MagicUseCase]
public class ProcessCompleteOrderUseCase(
    ICreateOrderUseCase createOrder,
    IProcessPaymentUseCase processPayment) : IProcessCompleteOrderUseCase
{
    public async Task<ProcessCompleteOrderResult> Execute(ProcessCompleteOrderRequest request)
    {
        // Step 1: Create the order
        var orderResult = await createOrder.Execute(new CreateOrderRequest
        {
            UserId = request.UserId,
            Items = request.Items,
        });

        // Step 2: Process payment
        var paymentResult = await processPayment.Execute(new ProcessPaymentRequest
        {
            OrderId = orderResult.OrderId,
            PaymentMethod = request.PaymentMethod,
        });

        // Return combined result
        return new ProcessCompleteOrderResult
        {
            OrderId = orderResult.OrderId,
            Total = orderResult.Total,
            PaymentSuccess = paymentResult.Success,
            Message = paymentResult.Message,
        };
    }
}