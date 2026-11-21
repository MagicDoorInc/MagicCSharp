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
    Task<LeaseDto?> GetLease(long leaseId, long propertyId);

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

Bodies go both ways as JSON with the conventions a MagicApp server uses (`MagicJsonConventions.HttpClientOptions` in
`MagicCSharp`): camelCase names, enums by name, and an `Optional<T>` sent as its value, as `null`, or left out of the
body when it holds no value, so a PATCH can say "do not touch this".

The URL comes from configuration under `UrlSetting`, else `DefaultUrl`; with neither, registration throws naming the
client and its missing setting. A failed call throws `MagicHttpClientException` with `StatusCode` and `ResponseBody`; its
message names the method, the path without its query string and the status, so logging it logs neither what the
service sent back nor the values the caller asked about.

Each client calls through a named `HttpClient`; `MagicHttpClientRegistration.HttpClientName(typeof(LeasesClient))`
is its name. Add a handler (authentication, retries, a test server's handler) by configuring that name after the
registration:

```csharp
services.AddLeasesClient(configuration);
services.AddHttpClient(MagicHttpClientRegistration.HttpClientName(typeof(LeasesClient)))
    .AddHttpMessageHandler<ServiceTokenHandler>();
```

**When to write it by hand:** a method with real logic (skipping the call for an empty list, streaming) has no route
attribute and is written by hand in `partial class LeasesClient`. A member written by hand there that keeps its route
attribute is left to the hand-written body, and the route check below still checks its path. Declare a constructor
there when the client needs more services; the generator then writes none.

To leave the generated client, write `class LeasesClient : MagicHttpClientBase, ILeasesClient` with every method and
its registration (copy them from the generated files), and remove `[MagicClient]`. Keeping the route attributes keeps
the route check.

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
