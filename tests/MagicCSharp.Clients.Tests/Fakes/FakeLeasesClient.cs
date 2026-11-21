using MagicCSharp.Clients.Tests.Leases;

namespace MagicCSharp.Clients.Tests.Fakes;

/// <summary>Scripts only the one call its tests make.</summary>
public class FakeLeasesClient : FakeLeasesClientBase
{
    public override Task<LeaseDto?> GetLease(long leaseId, long propertyId)
    {
        return Task.FromResult<LeaseDto?>(new LeaseDto { Id = leaseId, TenantName = "Ana Lee" });
    }
}
