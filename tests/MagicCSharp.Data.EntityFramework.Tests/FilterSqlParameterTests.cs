using MagicCSharp.Data.EntityFramework.Tests.Fixtures;
using MagicCSharp.Data.Utils;
using MagicCSharp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>
///     Each filter helper sends a filter value to the database as a SQL parameter, never written into the SQL text: a
///     written value would make every distinct filter value compile and cache a query of its own.
/// </summary>
public class FilterSqlParameterTests : RepositoryTestBase
{
    [Fact]
    public async Task ApplyListFilter_OneValue_SendsTheValueAsASqlParameter()
    {
        var sql = await SqlOf(shipments => shipments.ApplyListFilter(new List<long> { 424242 }, shipmentDal => shipmentDal.Id));

        AssertValueIsAParameter(sql, "424242");
    }

    [Fact]
    public async Task ApplyListFilter_SeveralValues_SendsTheValuesAsASqlParameter()
    {
        var sql = await SqlOf(shipments => shipments.ApplyListFilter(new List<long> { 424242, 434343 }, shipmentDal => shipmentDal.Id));

        AssertValueIsAParameter(sql, "424242");
        AssertValueIsAParameter(sql, "434343");
    }

    [Fact]
    public async Task ApplyListFilter_SeveralStrings_SendsTheStringsAsASqlParameter()
    {
        var sql = await SqlOf(shipments => shipments.ApplyListFilter(new List<string> { "TRK-4242", "TRK-4343" }, shipmentDal => shipmentDal.TrackingCode));

        AssertValueIsAParameter(sql, "TRK-4242");
        AssertValueIsAParameter(sql, "TRK-4343");
    }

    [Fact]
    public async Task ApplyNullableValueFilter_SendsTheValueAsASqlParameter()
    {
        var sql = await SqlOf(shipments => shipments.ApplyNullableValueFilter<ShipmentDal, long>(424242, shipmentDal => shipmentDal.TenantId));

        AssertValueIsAParameter(sql, "424242");
    }

    [Fact]
    public async Task ApplyComparableRangeFilter_SendsEachEndAsASqlParameter()
    {
        var range = new ComparableRange<long> { Start = 424242, End = 434343 };

        var sql = await SqlOf(shipments => shipments.ApplyComparableRangeFilter<ShipmentDal, long>(range, shipmentDal => shipmentDal.TenantId));

        AssertValueIsAParameter(sql, "424242");
        AssertValueIsAParameter(sql, "434343");
    }

    [Fact]
    public async Task ApplyStringNullableValueFilter_SendsTheSearchTextAsASqlParameter()
    {
        var sql = await SqlOf(shipments => shipments.ApplyStringNullableValueFilter(
            "needle-77", shipmentDal => shipmentDal.TrackingCode, StringFilterOperation.Contains));

        AssertValueIsAParameter(sql, "needle-77");
    }

    [Fact]
    public async Task ApplyNullablePresenceFilter_WritesNoValueIntoTheSql_ForRowsWithAValue()
    {
        var sql = await SqlOf(shipments => shipments.ApplyNullablePresenceFilter<ShipmentDal, DateTimeOffset>(true, shipmentDal => shipmentDal.Delivered));

        Assert.Contains("IS NOT NULL", WhereClause(sql));
        Assert.DoesNotContain("@", WhereClause(sql));
    }

    [Fact]
    public async Task ApplyNullablePresenceFilter_WritesNoValueIntoTheSql_ForRowsWithoutAValue()
    {
        var sql = await SqlOf(shipments => shipments.ApplyNullablePresenceFilter<ShipmentDal, DateTimeOffset>(false, shipmentDal => shipmentDal.Delivered));

        Assert.Contains("IS NULL", WhereClause(sql));
        Assert.DoesNotContain("@", WhereClause(sql));
    }

    private async Task<string> SqlOf(Func<IQueryable<ShipmentDal>, IQueryable<ShipmentDal>> filter)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        return filter(context.Shipments).ToQueryString();
    }

    private static void AssertValueIsAParameter(string sql, string value)
    {
        Assert.DoesNotContain(value, WhereClause(sql));
        Assert.Contains("@", WhereClause(sql));
    }

    // The part of the SQL the filter wrote; the select list before it names columns, not values.
    private static string WhereClause(string sql)
    {
        var whereStart = sql.IndexOf("WHERE", StringComparison.Ordinal);
        Assert.True(whereStart >= 0, "The query has no WHERE clause: " + sql);
        return sql.Substring(whereStart);
    }
}
