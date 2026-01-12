using OrderManagement.Business.UseCases.Orders;

namespace OrderManagement.Api.DTOs;

public record ProcessCompleteOrderResponseDto
{
    public long OrderId { get; init; }
    public decimal Total { get; init; }
    public bool PaymentSuccess { get; init; }
    public string Message { get; init; } = string.Empty;

    public static ProcessCompleteOrderResponseDto FromResult(ProcessCompleteOrderResult result)
    {
        return new ProcessCompleteOrderResponseDto
        {
            OrderId = result.OrderId,
            Total = result.Total,
            PaymentSuccess = result.PaymentSuccess,
            Message = result.Message,
        };
    }
}