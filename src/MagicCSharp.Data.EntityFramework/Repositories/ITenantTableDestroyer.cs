using MagicCSharp.Data.Repositories;

namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     A tenant destroyer that says which table it empties, so tenant deletion can order it against the others.
///     <para>
///         Foreign keys decide the order: a child row that still points at its parent stops the parent's delete. The
///         planner reads those keys from the context's EF model, and needs each destroyer to name its context and its
///         Dal to find them. The repository bases implement it; a destroyer that does not cannot be ordered, and
///         <see cref="TenantDeletion.TenantDeletionPlanner" /> refuses it rather than guess.
///     </para>
/// </summary>
public interface ITenantTableDestroyer : ITenantScopedDestroyer
{
    /// <summary>The context whose model holds the table.</summary>
    Type DbContextType { get; }

    /// <summary>The Dal mapped to the table this destroyer empties.</summary>
    Type DalType { get; }
}
