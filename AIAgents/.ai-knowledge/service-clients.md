# Service Clients

This guide owns calling another service over HTTP: the client interface, its registration, its fake in tests, and
the check that its routes exist on the server. Services also talk through events (`events-and-messaging.md`); a
client is for a question that needs an answer now.

## The interface is the client

The client lives in a library under `Libs/` (`mcs create-lib --name {Service}Client`) that the calling services
reference. The server never references its own client. With `MagicCSharp.Clients` and `MagicCSharp.Generators`, the
interface is all you write:

```csharp
[MagicClient("api/leases", UrlSetting = "Services:Leasing:Url", DefaultUrl = "http://leasing")]
public interface ILeasesClient
{
    [ClientGet("{leaseId}")]
    Task<LeaseDto?> GetLease(long leaseId);

    [ClientPost]
    Task<LeaseDto> CreateLease(CreateLeaseRequest request);

    [ClientPut("{leaseId}/renewal-reminder-sent")]
    Task MarkRenewalReminderSent(long leaseId);
}
```

The compiler writes `LeasesClient` and `services.AddLeasesClient(configuration)`; the calling service's DI module calls
that once. A use case depends on `ILeasesClient` like any other port.

- The route is the server's controller route; a `{name}` takes the parameter of that name.
- On a GET, object parameters go to the query string; on a POST, PUT, PATCH or DELETE the one object parameter is the
  JSON body, and scalars go to the query string. A `CancellationToken` parameter is passed on.
- `Task<T?>` returns `null` when the server answers 404 or 204; `Task<T>` throws. Return `T?` exactly when "not there"
  is an answer the caller handles.
- The URL comes from configuration under `UrlSetting`, else `DefaultUrl`.
- DTOs in a client library are the server's contract: records with primitives and ids, never the server's entities.

**Write a method by hand** when it does more than one call (skip the call for an empty list, page through results):
leave the route attribute off and write it in `partial class LeasesClient`. Everything else stays generated. In a
repository without the generators, write the class on `MagicHttpClientBase` and its registration as the generated
ones would be, and keep the route attributes so the route check still reads them.

## Its fake in tests

A test of a use case that calls a client uses a fake with only the members that test needs:

```csharp
[GenerateFakeBase(typeof(ILeasesClient))]
public abstract partial class FakeLeasesClientBase;

public class FakeLeasesClient : FakeLeasesClientBase
{
    public override Task<LeaseDto?> GetLease(long leaseId)
    {
        return Task.FromResult<LeaseDto?>(null);
    }
}
```

Every member the fake does not override throws `NotScriptedException` naming it, so a test that starts calling
something new fails loudly instead of passing on an empty answer.

## The server checks every client route

In the server's controller test project, which references the server and the client library:

```csharp
[Fact]
public void EveryLeasesClientRoute_IsServedByTheLeasingService()
{
    ClientRouteCheck.AssertEveryClientRouteIsServed(typeof(ILeasesClient).Assembly, typeof(LeasesController).Assembly);
}
```

It fails naming each client path or verb no controller serves, so a renamed route fails the server's build pipeline
instead of production. With it, a client needs no per-method path tests. Add the test when you add the client
library; change the client and the controller in the same change.
