using System.Reflection;
using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MagicCSharp.Data.EntityFramework.TenantDeletion;

/// <summary>
///     Decides the order tenant deletion empties tables in, and which tables it leaves alone.
///     <para>
///         A child row still pointing at its parent stops the parent's delete when the key is <c>Restrict</c> or
///         <c>NoAction</c>, and a destroyer deletes in one statement that no client-side cascade sees. So every destroyer
///         whose table holds a foreign key to another destroyer's table runs first, read from the context's EF model —
///         a key added later needs no registration edit. Where no key decides, the order destroyers were given in
///         stays: a child moves to just before its parent, nothing else moves. In a cycle of keys the destroyer given first runs
///         after the others.
///     </para>
///     <para>
///         A destroyer whose Dal is <c>[KeptOnTenantDeletion]</c> is left out and listed with its reason. A destroyer
///         that does not say which table it empties (<see cref="ITenantTableDestroyer" />), or names a Dal its context's
///         model does not map, cannot be ordered, and the plan throws naming it rather than guess.
///     </para>
/// </summary>
public sealed class TenantDeletionPlanner(IServiceProvider services)
{
    /// <summary>Orders <paramref name="destroyers" /> children first and sets kept tables aside.</summary>
    /// <exception cref="InvalidOperationException">A destroyer's table cannot be found, so it cannot be ordered.</exception>
    public TenantDeletionPlan PlanDeletion(IEnumerable<ITenantScopedDestroyer> destroyers)
    {
        var modelsByContext = new Dictionary<Type, IModel>();
        var tables = new List<TableToEmpty>();
        var kept = new List<KeptTenantTable>();
        var unorderableDestroyers = new List<string>();

        foreach (var destroyer in destroyers)
        {
            var tableDestroyer = destroyer as ITenantTableDestroyer;
            if (tableDestroyer == null)
            {
                unorderableDestroyers.Add($"{destroyer.GetType().Name} does not say which table it empties; implement {nameof(ITenantTableDestroyer)}");
                continue;
            }

            var keptOnTenantDeletion = tableDestroyer.DalType.GetCustomAttribute<KeptOnTenantDeletionAttribute>();
            if (keptOnTenantDeletion != null)
            {
                kept.Add(new KeptTenantTable { Destroyer = destroyer, DalType = tableDestroyer.DalType, Reason = keptOnTenantDeletion.Reason });
                continue;
            }

            var entityType = ModelOf(tableDestroyer.DbContextType, modelsByContext).FindEntityType(tableDestroyer.DalType);
            if (entityType == null)
            {
                unorderableDestroyers.Add($"{destroyer.GetType().Name} names {tableDestroyer.DalType.Name}, which {tableDestroyer.DbContextType.Name} does not map");
                continue;
            }

            tables.Add(new TableToEmpty { Destroyer = destroyer, DbContextType = tableDestroyer.DbContextType, EntityType = entityType });
        }

        if (unorderableDestroyers.Count > 0)
        {
            throw new InvalidOperationException("Tenant deletion cannot order these destroyers: " + string.Join("; ", unorderableDestroyers) + ".");
        }

        return new TenantDeletionPlan { Steps = OrderChildrenFirst(tables), Kept = kept };
    }

    /// <summary>
    ///     Takes the tables in the order given, and puts each table's children (and theirs) just before it, so a table
    ///     moves only when a key makes it. In a cycle of keys the table met first runs after the others; its key back
    ///     to them is the one ignored.
    /// </summary>
    private static IReadOnlyList<ITenantScopedDestroyer> OrderChildrenFirst(List<TableToEmpty> tables)
    {
        var childrenFirstOrder = new ChildrenFirstOrder(tables);
        foreach (var tableToEmpty in tables)
        {
            childrenFirstOrder.PlaceAfterItsChildren(tableToEmpty);
        }

        return childrenFirstOrder.Ordered;
    }

    /// <summary>
    ///     Whether <paramref name="child" />'s table holds a foreign key to <paramref name="parent" />'s. Two destroyers
    ///     of one table, and a table's key to itself, set no order.
    /// </summary>
    private static bool HasForeignKeyTo(TableToEmpty child, TableToEmpty parent)
    {
        if (child.DbContextType != parent.DbContextType || IsSameTable(child.EntityType, parent.EntityType))
        {
            return false;
        }

        return child.EntityType.GetForeignKeys().Any(foreignKey => IsSameTable(foreignKey.PrincipalEntityType, parent.EntityType));
    }

    /// <summary>The same entity type, or one of a hierarchy mapped to one table.</summary>
    private static bool IsSameTable(IEntityType first, IEntityType second)
    {
        return first.ClrType.IsAssignableFrom(second.ClrType) || second.ClrType.IsAssignableFrom(first.ClrType);
    }

    private IModel ModelOf(Type contextType, Dictionary<Type, IModel> modelsByContext)
    {
        if (modelsByContext.TryGetValue(contextType, out var knownModel))
        {
            return knownModel;
        }

        var factoryType = typeof(IDbContextFactory<>).MakeGenericType(contextType);
        var factory = services.GetService(factoryType) ??
                      throw new InvalidOperationException($"Tenant deletion needs the model of {contextType.Name}, and no IDbContextFactory<{contextType.Name}> is registered.");

        // Reading the model opens no connection.
        using var context = (DbContext)factoryType.GetMethod(nameof(IDbContextFactory<DbContext>.CreateDbContext))!.Invoke(factory, null)!;
        var model = context.Model;
        modelsByContext[contextType] = model;
        return model;
    }

    /// <summary>The order being built: each table goes in once, after the children it has among the given tables.</summary>
    private sealed class ChildrenFirstOrder(List<TableToEmpty> tables)
    {
        private readonly HashSet<TableToEmpty> placed = [];
        private readonly HashSet<TableToEmpty> beingPlaced = [];

        public List<ITenantScopedDestroyer> Ordered { get; } = [];

        public void PlaceAfterItsChildren(TableToEmpty tableToEmpty)
        {
            // Met again while its own children are being placed: a cycle, broken here.
            if (placed.Contains(tableToEmpty) || !beingPlaced.Add(tableToEmpty))
            {
                return;
            }

            foreach (var childTableToEmpty in tables.Where(candidate => HasForeignKeyTo(candidate, tableToEmpty)))
            {
                PlaceAfterItsChildren(childTableToEmpty);
            }

            placed.Add(tableToEmpty);
            Ordered.Add(tableToEmpty.Destroyer);
        }
    }

    /// <summary>A destroyer with the context and entity type of the table it empties.</summary>
    private sealed record TableToEmpty
    {
        public required ITenantScopedDestroyer Destroyer { get; init; }

        public required Type DbContextType { get; init; }

        public required IEntityType EntityType { get; init; }
    }
}
