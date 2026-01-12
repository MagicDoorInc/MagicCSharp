namespace MagicCSharp.Data.Dals;

/// <summary>
///     Interface for DAL objects that can transform to/from entity types.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TEntityEdit">The entity edit/update type.</typeparam>
public interface IDalTransform<TEntity, in TEntityEdit>
{
    /// <summary>
    ///     Transform this DAL object to an entity.
    /// </summary>
    /// <returns>The entity representation.</returns>
    public TEntity ToEntity();

    /// <summary>
    ///     Apply changes from an entity edit to this DAL object.
    /// </summary>
    /// <param name="entity">The entity edit containing updated values.</param>
    public void Apply(TEntityEdit entity);
}