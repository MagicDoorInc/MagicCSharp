using MagicCSharp.Clients.Tests.Leases;

namespace MagicCSharp.Clients.Tests.Fakes;

public interface ILeaseLookup
{
    string Region { get; set; }

    Task<LeaseDto?> FindLease(long leaseId);
}
