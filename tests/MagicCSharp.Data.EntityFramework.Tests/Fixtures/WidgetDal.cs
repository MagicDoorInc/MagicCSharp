using System.ComponentModel.DataAnnotations.Schema;
using MagicCSharp.Data.EntityFramework.Dals;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

[Table("widgets")]
public class WidgetDal : BaseIdDal<Widget, WidgetEdit>
{
    [Column("name")]
    public required string Name { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("quantity")]
    public required int Quantity { get; set; }

    [Column("status")]
    public required WidgetStatus Status { get; set; }

    public override Widget ToEntity()
    {
        return new Widget
        {
            Id = Id,
            Created = Created,
            Updated = Updated,
            Name = Name,
            Description = Description,
            Quantity = Quantity,
            Status = Status,
        };
    }

    public override void Apply(WidgetEdit edit)
    {
        Name = edit.Name;
        Description = edit.Description;
        Quantity = edit.Quantity;
        Status = edit.Status;
    }
}
