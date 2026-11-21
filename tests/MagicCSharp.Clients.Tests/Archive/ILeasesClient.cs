namespace MagicCSharp.Clients.Tests.Archive;

/// <summary>Has the short name of <see cref="Leases.ILeasesClient" /> and calls another service.</summary>
[MagicClient("api/archive", DefaultUrl = "http://archive.test")]
public interface ILeasesClient
{
    [ClientGet("count")]
    Task<int> GetArchivedLeaseCount();
}
