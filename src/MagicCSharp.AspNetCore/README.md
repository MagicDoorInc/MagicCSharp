# MagicCSharp.AspNetCore

What a MagicCSharp web service needs from ASP.NET, and nothing else: a request id on every request,
exceptions that leave as RFC 7807 problems instead of stack traces, and a startup check that resolves every
registration before the first request does.

Separate from the core package so a worker, a console app or a message consumer using `IMagicUseCase` does
not take a dependency on ASP.NET Core to get it. `MagicCSharp.App` makes all of these calls for you.

```csharp
builder.Services.AddMagicErrorHandling();

var app = builder.Build();
app.Services.ValidateServices(builder.Services);   // preflight

app.UseRequestId();
app.UseMagicErrorHandling();
app.MapControllers();
```

## Errors

A use case throws what it means; this is where that becomes a status code. Without it, a repository throwing
`NotFoundException` for a row that is not there becomes a 500 with a stack trace — the framework would define
the exception and then leave the caller to discover it as a server error.

| Exception | Status | Title |
|---|---|---|
| `HttpException` and its subclasses | whatever it carries | the class name minus `Exception` |
| `NotFoundException` from the domain | 404 | `NotFound` |
| `EntityConflictException` from the domain | 409 | `Conflict` |
| `EntityInvalidOperationException` from the domain | 422 | `InvalidOperation` |
| `ValidationException` (DataAnnotations) | 400 | `ValidationFailed` |
| `ArgumentException` | 400 | `BadRequest` |
| `OperationCanceledException` | 499 | `Cancelled` |
| anything else | 500 | `InternalServerError` |

The body is `application/problem+json`, with `instance` set to the path and a `requestId` extension carrying
the same id the `X-Request-ID` header does — so a bug report quoting the body can be found in the logs.
Outside Development a 500's detail is "An unexpected error occurred."; the exception itself goes to the log,
because an unhandled exception's message routinely contains a connection string, a file path or a row nobody
should see.

Logging is at the level the status implies: 5xx at Error with the exception, everything else at Information
in one line. That is why this is plain middleware rather than `UseExceptionHandler`, which logs every
exception it handles at Error — including the 404s and 409s that are routine — and makes an error dashboard
useless. Put `UseMagicErrorHandling` first so it covers everything after it.

`HttpException` is for the times a use case genuinely means a status code:

| | |
|---|---|
| `BadRequestException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `HttpNotFoundException` | 404 — for a controller with no domain call to make; otherwise prefer the domain's `NotFoundException`, which maps here anyway |
| `ConflictException` | 409 |
| `UnprocessableEntityException` | 422 |
| `TooManyRequestsException` | 429 |
| `HttpNotImplementedException` | 501 |

Derive your own for a status not listed. `ErrorHandlingModule.Describe(exception, shouldIncludeDetail)` is public,
so a test can assert the mapping without a web host.

## Request ids

```csharp
app.UseRequestId();
```

Accepts an `X-Request-ID` header from the caller and generates one when there isn't one, puts it on the
response, and makes it available through `IRequestIdHandler` for the life of the request — including across
`await`, which is the part a plain field cannot do. A supplied id is echoed back only if it is short and
printable; anything over 128 characters or carrying control characters is replaced, because it ends up in
every log line for the request.

Put it before `UseMagicErrorHandling`, so the error handler's own log line and the `requestId` in the problem
body carry the real id rather than Kestrel's connection counter.

Work that starts elsewhere sets its own. The Kafka listener sets a fresh request id per message, and the
event dispatcher opens a logging scope per event and per handler, so a log line from three handlers deep
still says which event it belongs to. `IRequestIdHandler.SetRequestId()` and `SetChildRequestId()` in the
core package do the same for a job or a fan-out of your own.

## Preflight

```csharp
app.Services.ValidateServices(builder.Services);
```

Resolves every registered service once, from a throwaway scope, before the application starts serving. A
missing or miswired dependency otherwise surfaces on the first request that happens to need it — which, in a
service with a rarely-used endpoint, can be days after the deploy that broke it. Resolving everything at
startup turns that into a failure the deploy itself reports.

It costs one construction of every singleton and scoped service. Call it once, after `Build()`.

## Note on the framework reference

This package uses `<FrameworkReference Include="Microsoft.AspNetCore.App" />` rather than the
`Microsoft.AspNetCore.Http.Abstractions` NuGet package. That package's last release is 2.2.0, from ASP.NET Core
2.2; referencing the shared framework gets the current types and adds nothing to the build output.

## Related packages

- [MagicCSharp.App](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp.App/README.md)
  — this, the core, events and scheduling, wired in two calls
- [MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp/blob/master/src/MagicCSharp/README.md)
  — use cases, `IRequestIdHandler`, `NotFoundException`

The whole picture: [github.com/MagicDoorInc/MagicCSharp](https://github.com/MagicDoorInc/MagicCSharp). MIT.
