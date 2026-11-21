using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.EntityFramework.Tests.Fixtures;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>Which DAL property is the tenant key: the one the assembly names, when it is a long.</summary>
public class TenantKeyPropertyTests
{
    [Fact]
    public void FindKeyPropertyName_IsNull_WhenTheTenantKeyPropertyIsNotALong()
    {
        // Act
        var tenantKeyPropertyName = TenantRows.FindKeyPropertyName(typeof(LabelDal));

        // Assert
        Assert.Null(tenantKeyPropertyName);
    }
}
