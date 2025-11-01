using OrderManagement.Business.UseCases.Orders;

namespace OrderManagement.Api.DTOs;

public record ProcessPaymentDto
{
    public string PaymentMethod { get; init; } = string.Empty;

    public ProcessPaymentRequest ToRequest(long orderId)
    {
        return new ProcessPaymentRequest
        {
            OrderId = orderId,
            PaymentMethod = PaymentMethod,
        };
    }
}