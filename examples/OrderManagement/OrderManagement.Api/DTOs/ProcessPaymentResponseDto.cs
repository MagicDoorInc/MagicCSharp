using OrderManagement.Business.UseCases.Orders;

namespace OrderManagement.Api.DTOs;

public record ProcessPaymentResponseDto
{
    public string Message { get; init; } = string.Empty;

    public static ProcessPaymentResponseDto FromResult(ProcessPaymentResult result)
    {
        return new ProcessPaymentResponseDto
        {
            Message = result.Message,
        };
    }
}