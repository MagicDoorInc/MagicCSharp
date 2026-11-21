# MagicCSharp.Clients

Typed HTTP clients for calling another service, written by the compiler. You write the interface; with
`MagicCSharp.Generators` referenced, every build writes the client class and its registration.

```bash
dotnet add package MagicCSharp.Clients
dotnet add package MagicCSharp.Generators
```

```csharp
[MagicClient("api/leases", UrlSetting = "Services:Leasing:Url", DefaultUrl = "http://leasing")]
public interface ILeasesClient
{
    [ClientGet("{leaseId}")]
    Task<LeaseDto?> GetLease(long leaseId, long companyId);

    [ClientPost]
    Task<LeaseDto> CreateLease(CreateLeaseRequest request);

    [ClientPut("{leaseId}/renewal-reminder-sent")]
    Task MarkRenewalReminderSent(long leaseId);
}
```

The compiler writes `LeasesClient` and `services.AddLeasesClient(configuration)`:

- `{leaseId}` takes the parameter of that name, escaped. Other simple parameters go to the query string.
- On a GET, object parameters (filters, paging) go to the query string; on a POST, PUT, PATCH or DELETE the one
  object parameter is the JSON body. A write without one sends `{}`, a DELETE without one sends no body.
- A `CancellationToken` parameter is passed on.
- `Task<T?>` returns `null` when the service answers 404 or 204; `Task<T>` throws on 404.

A method with real logic (skipping the call for an empty list, streaming) has no route attribute and is written by
hand in `partial class LeasesClient`. A member written by hand there that keeps its route attribute is left to the
hand-written body. Declare a constructor there when the client needs more services; the generator then writes none.

## Your own base and registration

By default the client derives from `MagicHttpClientBase` and registers with `AddMagicHttpClient`. A codebase with
its own client base (authentication, retries, logging) names it once, in the library its clients share:

```csharp
[assembly: MagicClientConvention(typeof(ServiceClientBase), typeof(ServiceClientRegistration), nameof(ServiceClientRegistration.AddServiceClient))]
```

The base offers what the generated code calls:

```csharp
Task<TResponse> SendRequestAsync<TResponse>(HttpMethod method, string path, object? body, CancellationToken cancellationToken);
Task SendRequestAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken);
string WithQuery(string path, params object?[] values);
bool IsNotFound(Exception exception);
```

and the registration is `static IServiceCollection AddServiceClient<TInterface, TClient>(this IServiceCollection
services, IConfiguration configuration, string? baseUrl)`.

## Tests

- `[GenerateFakeBase(typeof(ILeasesClient))]` on an `abstract partial class` in `MagicCSharp.Testing` gives a fake
  base whose every member throws "not scripted"; the fake overrides only what its tests use.
- `ClientRouteCheck.AssertEveryClientRouteIsServed` in `MagicCSharp.Testing.Clients` fails a server's test when a
  client path or verb matches no controller route.
