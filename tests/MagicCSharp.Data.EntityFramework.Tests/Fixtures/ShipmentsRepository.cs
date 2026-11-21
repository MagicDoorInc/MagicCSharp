using MagicCSharp.Data.EntityFramework.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>A repository whose <c>ApplyFilter</c> is generated; only the custom field's query is its own.</summary>
public partial class ShipmentsRepository(RepositoryServices<TestDbContext> repositoryServices)
    : BaseIdSoftDeleteRepository<TestDbContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices)
{
    private partial IQueryable<ShipmentDal> ApplyCustomFilter(IQueryable<ShipmentDal> query, ShipmentFilter filter)
    {
        var trackingCodeStart = filter.TrackingCodeStartsWith;
        if (trackingCodeStart == null)
        {
            return query;
        }

        return query.Where(shipmentDal => shipmentDal.TrackingCode.StartsWith(trackingCodeStart));
    }
}
