namespace MagicCSharp.Clients.Tests.Units;

[MagicClient("api/units", DefaultUrl = "http://leasing.test")]
public interface IUnitsClient
{
    [ClientGet("{unitId}/name")]
    Task<string> GetUnitName(long unitId);

    /// <summary>Written by hand, from the unit's name and the label settings the client's constructor takes.</summary>
    Task<string> GetUnitLabel(long unitId);
}
