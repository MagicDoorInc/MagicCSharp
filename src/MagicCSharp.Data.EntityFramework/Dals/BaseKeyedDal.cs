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

/// <summary>
///     A DAL keyed by an unguessable string key, with the primary key column already declared.
///     <para>
///         Use this rather than <see cref="BaseIdDal{TEntity,TEntityEdit}" /> when the key appears somewhere a
///         user can see it — a URL, an invite link, a webhook target. A Snowflake id encodes the time it was
///         issued and sits next to its neighbours, so exposing one leaks both when a record was created and
///         roughly how many exist; a random key leaks neither.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The entity type this DAL maps to.</typeparam>
/// <typeparam name="TEntityEdit">The entity edit type this DAL applies.</typeparam>
public abstract class BaseKeyDal<TEntity, TEntityEdit> : BaseDal<TEntity, TEntityEdit>, IDalKey
{
    /// <summary>
    ///     The string key, assigned by the application before insert.
    /// </summary>
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [StringLength(500)]
    [Column("key")]
    public required string Key { get; set; }
}
