using MagicCSharp.Cli.Infrastructure;

namespace MagicCSharp.Cli.Tests;

public class NamingTests
{
    [Theory]
    [InlineData("Order", "Orders")]
    [InlineData("Address", "Addresses")]
    [InlineData("Box", "Boxes")]
    [InlineData("Branch", "Branches")]
    [InlineData("Dish", "Dishes")]
    [InlineData("Category", "Categories")]
    [InlineData("Day", "Days")]        // vowel before y — not "Daies"
    [InlineData("Key", "Keys")]
    [InlineData("ApiKey", "ApiKeys")]
    public void Pluralize(string input, string isExpected)
    {
        Assert.Equal(isExpected, Naming.Pluralize(input));
    }

    [Theory]
    [InlineData("Order", "orders")]
    [InlineData("OrderLine", "order_lines")]
    [InlineData("ApiKey", "api_keys")]
    [InlineData("Category", "categories")]
    [InlineData("Address", "addresses")]
    public void ToTableName(string input, string isExpected)
    {
        Assert.Equal(isExpected, Naming.ToTableName(input));
    }

    // The generated code has to name its variables the way MagicCSharp.Analyzers (MCS0019) expects, or a
    // fresh repository does not build.
    [Theory]
    [InlineData("OrderDal", "orderDal")]
    [InlineData("ApiKeyDal", "apiKeyDal")]
    [InlineData("APIKeyDal", "apiKeyDal")]
    [InlineData("Order", "order")]
    [InlineData("SKU", "sku")]
    public void ToVariableName(string input, string isExpected)
    {
        Assert.Equal(isExpected, Naming.ToVariableName(input));
    }

    [Theory]
    [InlineData("Order", true)]
    [InlineData("O", true)]
    [InlineData("order", false)]
    [InlineData("Domains.Orders", false)]
    [InlineData("", false)]
    public void IsPascalWord(string input, bool isExpected)
    {
        Assert.Equal(isExpected, Naming.IsPascalWord(input));
    }

    [Theory]
    [InlineData("Domains.Orders", true)]
    [InlineData("Events", true)]
    [InlineData("Clients.Billing.Api", true)]
    [InlineData("domains.orders", false)]
    [InlineData("Domains..Orders", false)]
    public void IsDottedPascal(string input, bool isExpected)
    {
        Assert.Equal(isExpected, Naming.IsDottedPascal(input));
    }

    [Theory]
    [InlineData("shop", true)]
    [InlineData("order_management", true)]
    [InlineData("Shop", false)]
    [InlineData("order-management", false)]
    [InlineData("1shop", false)]
    public void IsDatabaseName(string input, bool isExpected)
    {
        Assert.Equal(isExpected, Naming.IsDatabaseName(input));
    }
}
