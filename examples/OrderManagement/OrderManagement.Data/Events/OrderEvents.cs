using MagicCSharp.Events.Events;

namespace OrderManagement.Data.Events;

public record OrderCreated : MagicEvent
{
    public long OrderId { get; set; }
    public long UserId { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public record PaymentProcessed : MagicEvent
{
    public long OrderId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}

public record OrderConfirmed : MagicEvent
{
    public long OrderId { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; }
}