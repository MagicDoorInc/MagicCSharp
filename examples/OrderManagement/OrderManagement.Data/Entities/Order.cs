using MagicCSharp.Infrastructure.Entities;

namespace OrderManagement.Data.Entities;

public record Order : OrderEdit, IMagicEntity, IIdEntity
{
    public long Id { get; set; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset Updated { get; init; }
}

public record OrderEdit
{
    public required long UserId { get; init; }
    public required List<OrderItem> Items { get; init; }
    public required decimal Total { get; init; }
    public required OrderStatus Status { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

public class OrderItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Subtotal => Quantity * Price;
}

public class OrderFilter
{
    public long? UserId { get; set; }
    public long? OrderId { get; set; }
}

public enum OrderStatus
{
    Pending,
    PaymentProcessing,
    Confirmed,
    Cancelled,
}