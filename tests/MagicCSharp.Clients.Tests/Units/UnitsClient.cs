namespace MagicCSharp.Clients.Tests.Units;

public partial class UnitsClient(IHttpClientFactory httpClientFactory, UnitLabelSettings unitLabelSettings) : MagicHttpClientBase(httpClientFactory)
{
    public async Task<string> GetUnitLabel(long unitId)
    {
        var unitName = await GetUnitName(unitId);
        return unitLabelSettings.Prefix + unitName;
    }
}
