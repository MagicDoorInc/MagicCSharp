using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Domains.Leases.Models.Entities;
using MagicCSharp.Data.EntityFramework.Dals;
using Microsoft.EntityFrameworkCore;

namespace Acme.Leasing.Data.EntityFramework.Dals;

[Table("notifications")]
public class NotificationDal : BaseKeyDal<Notification, NotificationEdit>
{
    [Required]
    [Column("lease_id")]
    public required long LeaseId { get; set; }

    [Required]
    [Column("recipient")]
    [StringLength(320)]
    public string Recipient { get; set; } = null!;

    [Required]
    [Column("subject")]
    [StringLength(200)]
    public string Subject { get; set; } = null!;

    [Required]
    [Column("body")]
    [StringLength(4000)]
    public string Body { get; set; } = null!;

    [ForeignKey(nameof(LeaseId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual LeaseDal Lease { get; set; } = null!;

    /// <summary>Row to entity. Read every column the entity exposes.</summary>
    public override Notification ToEntity()
    {
        return new Notification
        {
            Key = Key,
            LeaseId = LeaseId,
            Recipient = Recipient,
            Subject = Subject,
            Body = Body,
            Created = Created,
            Updated = Updated,
        };
    }

    /// <summary>
    ///     Edit onto row. Assign every writable field unconditionally — the repository decides whether
    ///     anything changed by asking the change tracker afterwards.
    /// </summary>
    public override void Apply(NotificationEdit edit)
    {
        Recipient = edit.Recipient;
        Subject = edit.Subject;
        Body = edit.Body;
    }

    /// <summary>Build a new row. The key comes from the edit: it names what the notification is about.</summary>
    public static NotificationDal From(NotificationEdit edit)
    {
        var notificationDal = new NotificationDal
        {
            Key = edit.Key,
            LeaseId = edit.LeaseId,
        };
        notificationDal.Apply(edit);
        return notificationDal;
    }
}
