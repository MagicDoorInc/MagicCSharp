using MagicCSharp.Clients;

namespace MagicCSharp.Testing.Clients.Tests.Clients;

/// <summary>Both methods are served by one action with an optional route value.</summary>
[MagicClient("api/reports")]
public interface IReportsClient
{
    [ClientGet]
    Task<string> GetLatestReport();

    [ClientGet("{reportId}")]
    Task<string> GetReport(long reportId);
}
