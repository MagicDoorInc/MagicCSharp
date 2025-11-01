# Separation of Concerns - Quick Reference

## Three-Layer Architecture

| Layer | Responsibility | HTTP Concerns | Business Logic | Technical Implementation |
|-------|---------------|---------------|----------------|-------------------------|
| **Controllers** | API routing & DTOs | ✅ Yes | ❌ No | ❌ No |
| **Use Cases** | Business workflows | ❌ No | ✅ Yes | ❌ No |
| **Services** | External APIs & protocols | ❌ No | ❌ No | ✅ Yes |

## Controllers - HTTP Layer Only

### Responsibilities
- Validate HTTP requests and extract parameters
- Map DTOs ↔ Entities
- Return HTTP status codes (200, 400, 404, 500, etc.)
- Manage authentication context
- Delegate ALL business logic to use cases

### Pattern
```csharp
[ApiController]
[Route("api/resource")]
public class MyController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MyDto>> Create(
        [FromServices] IMyUseCase useCase,
        [FromServices] IUserContext userContext,
        [FromBody] CreateRequest dto)
    {
        if (string.IsNullOrEmpty(userContext.UserId))
            return Unauthorized();

        try
        {
            var result = await useCase.Execute(new MyRequest
            {
                UserId = userContext.UserId,
                Data = dto.Data,
            });

            return Ok(new MyDto
            {
                Id = result.Id,
                Data = result.Data,
            });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create resource");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
```

### Rules
- ✅ **Do**: HTTP status codes, DTO mapping, authentication checks
- ❌ **Never**: Business logic, service coordination, data validation beyond HTTP input validation

## Use Cases - Business Logic Layer

### Responsibilities
- Implement business rules and workflows
- Coordinate multiple services and repositories
- Manage transactions and data consistency
- Handle business exceptions (NotFoundException, InvalidOperationException)
- Work with domain entities (NOT DTOs)

### Pattern
```csharp
[UseCase]
public class MyUseCase(
    IRepository repository,
    IExternalService externalService,
    IOtherUseCase otherUseCase) : IMyUseCase
{
    public async Task<MyResult> Execute(MyRequest request)
    {
        // 1. Validate business rules
        var entity = await repository.Get(request.Id);
        if (entity == null)
            throw new NotFoundException($"Entity {request.Id} not found");

        if (!entity.IsActive)
            throw new InvalidOperationException("Entity is not active");

        // 2. Coordinate services
        var externalData = await externalService.FetchData(entity.ExternalId);

        // 3. Apply business logic
        var processed = ProcessBusinessRules(entity, externalData);

        // 4. Persist changes
        await repository.Update(entity.Id, processed);

        // 5. Trigger workflows
        await otherUseCase.Execute(new OtherRequest { EntityId = entity.Id });

        return new MyResult { Success = true, Data = processed };
    }
}
```

### Rules
- ✅ **Do**: Business workflows, rule validation, service coordination
- ❌ **Never**: HTTP concerns (status codes, DTOs), technical protocols (OAuth, HTTP headers)

## Services - Technical Layer

### Responsibilities
- Communicate with external APIs (Google, OpenAI, Stripe, etc.)
- Handle technical protocols (OAuth, HTTP, encryption)
- Provide utility functions (encryption, formatting)
- NO business logic or decisions

### Pattern
```csharp
public class GoogleOAuthService(
    GoogleOAuthConfiguration config,
    HttpClient httpClient) : IGoogleOAuthService
{
    public async Task<TokenResponse> ExchangeCodeForTokens(string code)
    {
        // Pure technical implementation - HTTP call to Google
        var request = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("client_id", config.ClientId),
            new KeyValuePair<string, string>("client_secret", config.ClientSecret),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
        });

        var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", request);
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<TokenResponse>(
            await response.Content.ReadAsStringAsync());
    }
}
```

### Rules
- ✅ **Do**: External API calls, protocol implementation, data transformation
- ❌ **Never**: Business decisions, workflow orchestration, state management

## Decision Matrix: Which Layer?

| Task | Layer |
|------|-------|
| Validate OAuth state token | **Use Case** (business rule) |
| Make HTTP call to Google OAuth | **Service** (technical) |
| Return 401 Unauthorized | **Controller** (HTTP) |
| Decide whether to create or update integration | **Use Case** (business logic) |
| Encrypt access token | **Service** (technical utility) |
| Map Entity to DTO | **Controller** (HTTP concern) |
| Coordinate multiple data sources | **Use Case** (orchestration) |
| Parse JSON from API response | **Service** (technical) |
| Handle NotFoundException | **Controller** (HTTP error handling) |
| Throw NotFoundException | **Use Case** (business exception) |

## Data Flow Example

```
HTTP Request
    ↓
Controller
    ├── Extract parameters from HTTP
    ├── Validate authentication
    ├── Map DTO → Request
    ↓
Use Case
    ├── Validate business rules
    ├── Call Service A (technical operation)
    ├── Call Service B (technical operation)
    ├── Call Repository (data access)
    ├── Call Other Use Case (business workflow)
    ├── Return business result
    ↓
Controller
    ├── Map Result → DTO
    ├── Return HTTP status + response
    ↓
HTTP Response
```

## Anti-Patterns to Avoid

### ❌ Business Logic in Controller
```csharp
// DON'T DO THIS
[HttpPost]
public async Task<ActionResult> Create([FromServices] IRepository repo)
{
    var entity = await repo.Get(id);
    if (entity == null || !entity.IsActive)  // ❌ Business logic
        return BadRequest();

    await repo.Update(entity.Id, edit);  // ❌ Direct repository access
    return Ok();
}
```

### ❌ HTTP Concerns in Use Case
```csharp
// DON'T DO THIS
public class MyUseCase
{
    public async Task<IActionResult> Execute(MyRequest request)  // ❌ IActionResult
    {
        if (somethingWrong)
            return NotFound();  // ❌ HTTP status codes
    }
}
```

### ❌ Business Logic in Service
```csharp
// DON'T DO THIS
public class GoogleOAuthService
{
    public async Task CompleteOAuth(string code)
    {
        var tokens = await ExchangeCode(code);

        if (ShouldCreateNewIntegration())  // ❌ Business decision
            await CreateIntegration();     // ❌ Business workflow
    }
}
```

## Error Handling by Layer

```csharp
// Controller - HTTP errors
catch (UnauthorizedAccessException) { return Unauthorized(); }
catch (NotFoundException ex) { return NotFound(new { error = ex.Message }); }
catch (Exception ex) { return StatusCode(500, new { error = "Internal error" }); }

// Use Case - Business exceptions
if (integration == null)
    throw new NotFoundException($"Integration {id} not found");

if (!integration.IsActive)
    throw new InvalidOperationException("Integration is not active");

// Service - Technical exceptions
try
{
    var response = await httpClient.GetAsync(url);
    response.EnsureSuccessStatusCode();
}
catch (HttpRequestException ex)
{
    throw new ExternalServiceException("API call failed", ex);
}
```

## Key Principles

1. **Controllers**: Thin layer, only HTTP concerns
2. **Use Cases**: Orchestrate workflows, implement business rules
3. **Services**: Pure technical implementations, no business decisions
4. **Never mix layers**: Each layer has one clear responsibility
5. **Data flows down**: Controller → Use Case → Service → External API
6. **Exceptions flow up**: Service → Use Case → Controller → HTTP response
