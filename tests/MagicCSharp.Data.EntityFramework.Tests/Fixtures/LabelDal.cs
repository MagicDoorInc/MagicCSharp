namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>A DAL whose <c>TenantId</c> is text, so it cannot be the assembly's tenant key.</summary>
public class LabelDal
{
    public string TenantId { get; set; } = "";
}
