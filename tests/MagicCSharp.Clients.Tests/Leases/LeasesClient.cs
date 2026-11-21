namespace MagicCSharp.Clients.Tests.Leases;

public partial class LeasesClient
{
    public async Task<IReadOnlyList<LeaseDto>> GetLeases(IReadOnlyList<long> leaseIds)
    {
        if (leaseIds.Count == 0)
        {
            return [];
        }

        return await GetLeases(new LeaseFilter { LeaseIds = leaseIds }, new PageRequest { Page = 1, PageSize = leaseIds.Count });
    }

    public Task<IReadOnlyList<string>> GetDocumentNames(long leaseId)
    {
        IReadOnlyList<string> documentNames = ["lease-" + leaseId + ".pdf"];
        return Task.FromResult(documentNames);
    }
}
