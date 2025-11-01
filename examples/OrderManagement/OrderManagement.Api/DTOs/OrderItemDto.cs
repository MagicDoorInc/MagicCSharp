namespace OrderManagement.Api.DTOs;

public record OrderItemDto
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Price { get; init; }
}