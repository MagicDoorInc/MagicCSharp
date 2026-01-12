using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MagicCSharp.Data.Dals;

/// <summary>
///     Base class for Data Access Layer (DAL) objects with automatic timestamp tracking.
/// </summary>
/// <typeparam name="TEntity">The entity type this DAL represents.</typeparam>
/// <typeparam name="TEntityEdit">The entity edit/update type.</typeparam>
public abstract class BaseDal<TEntity, TEntityEdit> : IDal, IDalTransform<TEntity, TEntityEdit>
{
    /// <summary>
    ///     When this DAL object was created.
    /// </summary>
    [Required]
    [Column("created")]
    public DateTimeOffset Created { get; set; }

    /// <summary>
    ///     When this DAL object was last updated.
    /// </summary>
    [Required]
    [Column("updated")]
    public DateTimeOffset Updated { get; set; }

    /// <summary>
    ///     Transform this DAL object to its entity representation.
    /// </summary>
    /// <returns>The entity.</returns>
    public abstract TEntity ToEntity();

    /// <summary>
    ///     Apply changes from an entity edit to this DAL object.
    /// </summary>
    /// <param name="entity">The entity edit containing updated values.</param>
    public abstract void Apply(TEntityEdit entity);
}