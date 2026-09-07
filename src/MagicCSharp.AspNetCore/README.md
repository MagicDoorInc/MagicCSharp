# MagicCSharp.AspNetCore

ASP.NET Core integration for MagicCSharp. Currently one thing: request ID middleware.

Separate from the core package so a console app, a worker or a message consumer using `IMagicUseCase` does not
take a dependency on ASP.NET Core to get it.

## Request IDs

```csharp
app.UseRequestId();
```

Accepts an `X-Request-ID` header from the caller and generates one when there isn't one, puts it on the
response, and makes it available through `IRequestIdHandler` for the life of the request — including across
`await`, which is the part a plain field cannot do.

`AsyncEventDispatcher` sets a request ID of its own per event, derived from the event ID, and a child ID per
handler, so a log line from three handlers deep still traces back to the request that started it.

## Note on the framework reference

This package uses `<FrameworkReference Include="Microsoft.AspNetCore.App" />` rather than the
`Microsoft.AspNetCore.Http.Abstractions` NuGet package. That package's last release is 2.2.0, from ASP.NET Core
2.2; referencing the shared framework gets the current types and adds nothing to the build output.
