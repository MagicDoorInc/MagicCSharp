using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.Dals;
using Microsoft.EntityFrameworkCore;
using OrderManagement.Data.Domain.Entities;

namespace OrderManagement.Data.EntityFramework.DALs;

[Table("orders")]
[Index(nameof(UserId))]
public class OrderDal : BaseDal<Order, OrderEdit>, IDalId
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("user_id")]
    public long UserId { get; set; }

    [Required]
    [Column("total")]
    [Precision(18, 2)]
    public decimal Total { get; set; }

    [Required]
    [Column("status")]
    public OrderStatus Status { get; set; }

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation property for items (one-to-many)
    public List<OrderItemDal> Items { get; set; } = new List<OrderItemDal>();

    // 1. Convert DAL → Entity
    public override Order ToEntity()
    {
        return new Order
        {
            Id = Id,
            UserId = UserId,
            Items = Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    Price = i.Price,
                })
                .ToList(),
            Total = Total,
            Status = Status,
            CompletedAt = CompletedAt,
            Created = Created,
            Updated = Updated,
        };
    }

    // 2. Update DAL from Edit
    public override void Apply(OrderEdit edit)
    {
        UserId = edit.UserId;
        Total = edit.Total;
        Status = edit.Status;
        CompletedAt = edit.CompletedAt?.ToUniversalTime();
        // Note: Items are handled in repository hooks, not here
    }

    // 3. Create DAL from Edit
    public static OrderDal From(OrderEdit edit, long id)
    {
        var dal = new OrderDal { Id = id };
        dal.Apply(edit);
        return dal;
    }
}

[Table("order_items")]
[Index(nameof(OrderId))]
public class OrderItemDal
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("order_id")]
    public long OrderId { get; set; }

    [Required]
    [Column("product_id")]
    public long ProductId { get; set; }

    [Required]
    [Column("product_name")]
    [StringLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [Column("quantity")]
    public int Quantity { get; set; }

    [Required]
    [Column("price")]
    [Precision(18, 2)]
    public decimal Price { get; set; }

    // Foreign key navigation
    public OrderDal Order { get; set; } = null!;
}
