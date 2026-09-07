using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Models;
using MagicCSharp.Infrastructure.KeyGen;
using MagicCSharp.Data.Repositories;
using MagicCSharp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

public interface IWidgetsRepository :
    IRepository<Widget, long, WidgetEdit, WidgetFilter>,
    IPaginatedRepository<Widget, WidgetFilter>;

/// <summary>
///     What a consumer writes: derive, map the filter, build the DAL. Everything else is inherited, and
///     everything else is what these tests are actually exercising.
/// </summary>
public class WidgetsRepository(
    IDbContextFactory<TestDbContext> contextFactory,
    IClock clock,
    ILoggerFactory loggerFactory)
    : BaseIdPaginatedRepository<TestDbContext, WidgetDal, Widget, WidgetFilter, WidgetEdit>(contextFactory, clock, loggerFactory),
        IWidgetsRepository
{
    protected override WidgetDal CreateDal(WidgetEdit edit)
    {
        return new WidgetDal
        {
            Id = Ids.GetId(),
            Name = edit.Name,
            Description = edit.Description,
            Quantity = edit.Quantity,
            Status = edit.Status,
        };
    }

    protected override IQueryable<WidgetDal> ApplyFilter(IQueryable<WidgetDal> query, WidgetFilter filter)
    {
        if (filter.Ids != null)
        {
            query = query.Where(widget => filter.Ids.Contains(widget.Id));
        }

        if (filter.Name != null)
        {
            query = query.Where(widget => widget.Name == filter.Name);
        }

        if (filter.DescriptionContains != null)
        {
            query = query.Where(widget => widget.Description != null && widget.Description.Contains(filter.DescriptionContains));
        }

        if (filter.Status != null)
        {
            query = query.Where(widget => widget.Status == filter.Status);
        }

        if (filter.MinimumQuantity != null)
        {
            query = query.Where(widget => widget.Quantity >= filter.MinimumQuantity);
        }

        return query;
    }

    /// <summary>Ids come from the application, not a sequence — see BaseIdDal.</summary>
    public required IKeyGenService Ids { get; init; }
}
