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
    public void Pluralize(string input, string expected)
    {
        Assert.Equal(expected, Naming.Pluralize(input));
    }

    [Theory]
    [InlineData("Order", "orders")]
    [InlineData("OrderLine", "order_lines")]
    [InlineData("ApiKey", "api_keys")]
    [InlineData("Category", "categories")]
    [InlineData("Address", "addresses")]
    public void ToTableName(string input, string expected)
    {
        Assert.Equal(expected, Naming.ToTableName(input));
    }

    [Theory]
    [InlineData("Order", true)]
    [InlineData("O", true)]
    [InlineData("order", false)]
    [InlineData("Domains.Orders", false)]
    [InlineData("", false)]
    public void IsPascalWord(string input, bool expected)
    {
        Assert.Equal(expected, Naming.IsPascalWord(input));
    }

    [Theory]
    [InlineData("Domains.Orders", true)]
    [InlineData("Events", true)]
    [InlineData("Clients.Billing.Api", true)]
    [InlineData("domains.orders", false)]
    [InlineData("Domains..Orders", false)]
    public void IsDottedPascal(string input, bool expected)
    {
        Assert.Equal(expected, Naming.IsDottedPascal(input));
    }

    [Theory]
    [InlineData("shop", true)]
    [InlineData("order_management", true)]
    [InlineData("Shop", false)]
    [InlineData("order-management", false)]
    [InlineData("1shop", false)]
    public void IsDatabaseName(string input, bool expected)
    {
        Assert.Equal(expected, Naming.IsDatabaseName(input));
    }
}
