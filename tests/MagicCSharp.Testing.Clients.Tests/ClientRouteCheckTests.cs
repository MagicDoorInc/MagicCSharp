using MagicCSharp.Testing.Clients.Tests.Clients;

namespace MagicCSharp.Testing.Clients.Tests;

public class ClientRouteCheckTests
{
    // The clients and the controllers live in this one test assembly.
    private static readonly ClientRouteCheckResult ClientRouteCheckResult =
        ClientRouteCheck.CheckClientRoutes(typeof(IOrdersClient).Assembly, typeof(IOrdersClient).Assembly);

    [Fact]
    public void CheckClientRoutes_DiscoversTheClientAndServerRoutes()
    {
        // Assert
        Assert.Equal([
            "IDriftedOrdersClient.CancelOrder DELETE api/orders/{orderId}",
            "IDriftedOrdersClient.GetOrderHistory GET api/orders/{orderId}/history",
            "IHealthClient.GetHealth GET health",
            "IInvoicesClient.GetInvoice GET billing/invoices/{invoiceId}",
            "IOrdersClient.CreateOrder POST api/orders",
            "IOrdersClient.GetOrder GET api/orders/{orderId}",
            "IOrdersClient.UpdateLine PUT api/orders/{orderId}/lines/{lineId}",
            "IReportsClient.GetLatestReport GET api/reports",
            "IReportsClient.GetReport GET api/reports/{reportId}",
        ], ClientRouteCheckResult.ClientRoutes.Select(clientRoute => Describe(clientRoute)).Order(StringComparer.Ordinal));
        Assert.Equal([
            "Invoices.GetInvoice GET billing/invoices/{invoiceId}",
            "Orders.CreateOrder POST api/Orders",
            "Orders.GetOrder GET api/Orders/{id:long}",
            "Orders.UpdateLine PUT api/Orders/{id:long}/lines/{lineNumber}",
            "Reports.GetReport GET api/reports/{reportId?}",
            "Status.GetHealth GET health",
        ], ClientRouteCheckResult.ServerRoutes.Select(serverRoute => Describe(serverRoute)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CheckClientRoutes_ReportsAClientPathNoServerRouteServes_NamingClientMethodVerbAndPath()
    {
        // Assert
        Assert.Contains("IDriftedOrdersClient.GetOrderHistory GET api/orders/{orderId}/history", ClientRouteCheckResult.UnservedClientRoutes.Select(clientRoute => Describe(clientRoute)));
    }

    [Fact]
    public void CheckClientRoutes_ReportsAVerbTheServerRouteDoesNotAccept()
    {
        // Assert
        Assert.Contains("IDriftedOrdersClient.CancelOrder DELETE api/orders/{orderId}", ClientRouteCheckResult.UnservedClientRoutes.Select(clientRoute => Describe(clientRoute)));
    }

    [Fact]
    public void CheckClientRoutes_MatchesAcrossParameterNamesAndConstraints()
    {
        // Assert
        AssertAllServed(nameof(IOrdersClient));
    }

    [Fact]
    public void CheckClientRoutes_ResolvesAreaTokens()
    {
        // Assert
        AssertAllServed(nameof(IInvoicesClient));
    }

    [Fact]
    public void CheckClientRoutes_ResolvesRootedRoutes()
    {
        // Assert
        AssertAllServed(nameof(IHealthClient));
    }

    [Fact]
    public void CheckClientRoutes_MatchesAnOptionalServerValue_WithAndWithoutIt()
    {
        // Assert
        Assert.Equal(2, ClientRouteCheckResult.ClientRoutes.Count(clientRoute => clientRoute.Client == nameof(IReportsClient)));
        AssertAllServed(nameof(IReportsClient));
    }

    [Fact]
    public void AssertEveryClientRouteIsServed_Throws_ListingEachUnservedClientRoute()
    {
        // Act
        var invalidOperationException = Assert.Throws<InvalidOperationException>(() =>
            ClientRouteCheck.AssertEveryClientRouteIsServed(typeof(IOrdersClient).Assembly, typeof(IOrdersClient).Assembly));

        // Assert
        Assert.Equal("""
                     No controller action serves these client routes:
                     IDriftedOrdersClient.GetOrderHistory: GET api/orders/{orderId}/history
                     IDriftedOrdersClient.CancelOrder: DELETE api/orders/{orderId}
                     """.Replace("\r", ""), invalidOperationException.Message);
    }

    [Fact]
    public void AssertEveryClientRouteIsServed_Throws_WhenNoClientRouteIsFound()
    {
        // Act
        var invalidOperationException = Assert.Throws<InvalidOperationException>(() =>
            ClientRouteCheck.AssertEveryClientRouteIsServed(typeof(ClientRouteCheck).Assembly, typeof(IOrdersClient).Assembly));

        // Assert
        Assert.Equal("MagicCSharp.Testing.Clients has no [MagicClient] interface with a client route attribute.", invalidOperationException.Message);
    }

    private static void AssertAllServed(string client)
    {
        Assert.Contains(ClientRouteCheckResult.ClientRoutes, clientRoute => clientRoute.Client == client);
        Assert.DoesNotContain(ClientRouteCheckResult.UnservedClientRoutes, clientRoute => clientRoute.Client == client);
    }

    private static string Describe(ClientRoute clientRoute)
    {
        return clientRoute.Client + "." + clientRoute.Method + " " + clientRoute.HttpMethod + " " + clientRoute.Path;
    }

    private static string Describe(ServerRoute serverRoute)
    {
        return serverRoute.Controller + "." + serverRoute.Action + " " + string.Join(",", serverRoute.HttpMethods) + " " + serverRoute.Path;
    }
}
