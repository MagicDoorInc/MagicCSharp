using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MagicCSharp.Data.EntityFramework.Dals;

/// <summary>
///     A DAL keyed by a Snowflake id, with the primary key column already declared.
///     <para>
///         <c>DatabaseGeneratedOption.None</c> is the point of this class: the id comes from
///         <c>IKeyGenService</c> before the row is inserted, not from a database sequence. That is what lets a
///         caller know an entity's id without a round trip, and what keeps ids unique across instances and
///         databases. Leaving it to the database identity column would break both.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The entity type this DAL maps to.</typeparam>
/// <typeparam name="TEntityEdit">The entity edit type this DAL applies.</typeparam>
public abstract class BaseIdDal<TEntity, TEntityEdit> : BaseDal<TEntity, TEntityEdit>, IDalId
{
    /// <summary>
    ///     The Snowflake id, assigned by the application before insert.
    /// </summary>
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("id")]
    public required long Id { get; set; }
}
