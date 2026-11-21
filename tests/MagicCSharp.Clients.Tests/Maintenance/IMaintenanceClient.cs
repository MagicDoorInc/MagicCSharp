namespace MagicCSharp.Clients.Tests.Maintenance;

/// <summary>A client that names no base URL, neither a setting nor a default.</summary>
[MagicClient("api/maintenance")]
public interface IMaintenanceClient
{
    [ClientGet("open-count")]
    Task<int> GetOpenRequestCount();
}
