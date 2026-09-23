# API and Controller Conventions

This guide owns controllers and DTOs. A controller is a translator: HTTP in, a use case call, HTTP out.

## Where they live

In the domain's `App/Default` project: `Controllers/` and, beside it, `Models/` for that controller's DTOs.
DTOs are never shared between domains — each endpoint's contract changes for its own reasons.

## What a controller does

```csharp
[ApiController]
[Route("charges")]
public class ChargesController : ControllerBase
{
    [HttpPost("{id:long}/pay")]
    public async Task<ChargeDto> PayCharge(
        [FromRoute] long id,
        [FromServices] IPayChargeUseCase payCharge)
    {
        var charge = await payCharge.Execute(id);
        return ChargeDto.FromEntity(charge);
    }
}
```

1. Bind the input, converting DTO → request (`request.ToRequest()`).
2. Call **one** use case, taken with `[FromServices]`.
3. Convert the result → DTO (`Dto.FromEntity(...)`).

A controller **never** decides anything, touches a repository, publishes an event or catches an exception.
Typed exceptions travel to the error middleware, which answers with problem+json (the status table is in
`use-case-patterns.md` → Errors).

## Parameters

- Every parameter states its source: `[FromRoute]`, `[FromQuery]`, `[FromBody]`, `[FromServices]` — in that
  order, services last.
- Query filters come as one `{Resource}FilterDto` with a `ToFilter()` method, never loose scalars. Pagination
  is a separate `[FromQuery] PaginationRequestDto? pagination` — never inherited into the filter.
- Route ids are `long` with a route constraint: `{id:long}`.

## DTOs

- Records. A DTO maps itself: `static FromEntity(entity)` out, `ToRequest()` / `ToFilter()` in.
- **Ids are strings in JSON.** Snowflake ids exceed what a JavaScript number holds. Convert inbound ids with
  `Parser.ToId()` / `ToIds()` from `Libs/Web`, which answer a bad id with 400 — never `long.Parse`.
- Calendar dates are `DateOnly`; instants are `DateTimeOffset`.
- Validate shape with attributes (`[Required]`, `[StringLength]`, `[EmailAddress]`); business rules belong to
  the use case.

## Return types

| Endpoint | Returns |
|---|---|
| Get one that must exist | `Task<TDto>` — the missing case throws 404 |
| List | `Task<PaginationDto<TDto>>` |
| Create, update, command with a result | `Task<TDto>` |
| Command with nothing to return | `Task` (204) |

Never `IActionResult`, `ActionResult<T>`, `Ok(...)` or `NotFound()`.

## Authentication

This example has none. A real service puts its auth policy on the controllers and adds a layer of use cases
between controllers and these for access checks and caller identity — see `use-case-patterns.md`.
