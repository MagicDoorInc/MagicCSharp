namespace MagicCSharp.Clients.Tests.Leases;

[MagicClient("api/leases", UrlSetting = "Services:Leasing:Url", DefaultUrl = "http://leasing.test")]
public partial interface ILeasesClient
{
    [ClientGet("{leaseId}")]
    Task<LeaseDto?> GetLease(long leaseId, long companyId);

    [ClientGet("by-tenant/{tenantName}")]
    Task<LeaseDto> GetLeaseByTenant(string tenantName);

    [ClientGet]
    Task<IReadOnlyList<LeaseDto>> GetLeases(LeaseFilter filter, PageRequest page);

    /// <summary>Written by hand: an empty list asks the service nothing.</summary>
    Task<IReadOnlyList<LeaseDto>> GetLeases(IReadOnlyList<long> leaseIds);

    [ClientPost]
    Task<LeaseDto> CreateLease(CreateLeaseRequest request);

    [ClientPut("{leaseId}")]
    Task<LeaseDto> UpdateLease(long leaseId, UpdateLeaseRequest request);

    [ClientPatch("{leaseId}/rent")]
    Task ChangeRent(long leaseId, ChangeRentRequest request);

    [ClientDelete("{leaseId}/notes")]
    Task DeleteNotes(long leaseId, DeleteNotesRequest request);

    [ClientPut("{leaseId}/renewal-reminder-sent")]
    Task MarkRenewalReminderSent(long leaseId);

    [ClientDelete("{leaseId}")]
    Task DeleteLease(long leaseId);

    [ClientGet("{leaseId}/balance")]
    Task<decimal> GetBalance(long leaseId, CancellationToken cancellationToken);

    /// <summary>Written by hand, keeping its route attribute so the route check still covers it.</summary>
    [ClientGet("{leaseId}/documents")]
    Task<IReadOnlyList<string>> GetDocumentNames(long leaseId);
}
