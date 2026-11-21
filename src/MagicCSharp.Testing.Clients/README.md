# MagicCSharp.Testing.Clients

Keeps `[MagicClient]` interfaces honest against the service they call. A client's path is written in its route
attributes and again on the server's controller; this check fails the server's tests when the two drift, so the
client needs no per-method path tests.

```bash
dotnet add package MagicCSharp.Testing.Clients
```

In the server's controller test project, which references both the server and its client library (the server
itself never references its own client):

```csharp
[Fact]
public void EveryLeasesClientRoute_IsServedByTheLeasingService()
{
    ClientRouteCheck.AssertEveryClientRouteIsServed(typeof(ILeasesClient).Assembly, typeof(LeasesController).Assembly);
}
```

The server's routes come from MVC's own action model, so area, controller and action tokens, inherited routes
and `~/` read as they do in production; no host starts and no controller is constructed. Paths compare
case-insensitively, `{leaseId}` matches `{id:long}`, and an optional `{id?}` matches with or without it. The check
also fails when either assembly has no routes at all, which means the test named the wrong assembly.

`ClientRouteCheck.CheckClientRoutes` returns what was found on both sides for a test that asserts on it itself.
