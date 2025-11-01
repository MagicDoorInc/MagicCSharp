# Coding Standards - Quick Reference

## Core C# Rules

### 1. Primary Constructors (Always)
```csharp
// ✅ Correct
public class MyService(IRepository repository, ILogger logger) : IMyService
{
    // Use injected parameters directly
}

// ❌ Incorrect - Traditional constructor
public class MyService : IMyService {
    private readonly IRepository repository;
    public MyService(IRepository repository) { this.repository = repository; }
}
```

### 2. Explicit Type in `new` (Never use `new()`)
```csharp
// ✅ Correct
var list = new List<string>();
var dict = new Dictionary<long, User>();

// ❌ Incorrect
var list = new();
var dict = new();
```

### 3. Trailing Commas (Always)
```csharp
// ✅ Correct
var items = new[]
{
    item1,
    item2,
    item3,  // ← trailing comma
};

await repository.Create(
    entity,
    cancellationToken,  // ← trailing comma
);
```

### 4. No Underscore Prefix (Never)
```csharp
// ✅ Correct
private readonly IRepository repository;
private static readonly Logger Log;

// ❌ Incorrect
private readonly IRepository _repository;
private static readonly Logger _log;
```

### 5. No "Async" Suffix (Never)
```csharp
// ✅ Correct
Task<User> GetUser(long userId);
Task SendEmail(Email email);

// ❌ Incorrect
Task<User> GetUserAsync(long userId);
Task SendEmailAsync(Email email);
```

## Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Classes | PascalCase | `UserService` |
| Interfaces | PascalCase with `I` prefix | `IUserService` |
| Methods | PascalCase | `GetUser` |
| Parameters | camelCase | `userId` |
| Local variables | camelCase | `userEmail` |
| Private fields | camelCase (no underscore) | `repository` |
| Constants | PascalCase | `MaxRetries` |
| Enums | PascalCase (type and values) | `enum Status { Active, Inactive }` |

## Use Case Pattern

### Simple Use Cases (Avoid Wrapper Objects)
```csharp
// ✅ Correct - Direct parameters (no useless wrappers)
public interface IGetUserUseCase : IUseCase
{
    Task<User> Execute(string userId);
}

[UseCase]
public class GetUserUseCase(IUserRepository repository) : IGetUserUseCase
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public async Task<User> Execute(string userId)
    {
        // Implementation
    }
}

// ❌ Incorrect - Unnecessary wrapper for single parameter
public record GetUserRequest { public string UserId { get; init; } }
public interface IGetUserUseCase : IUseCase
{
    Task<User> Execute(GetUserRequest request);  // Useless wrapper!
}
```

### Complex Use Cases (Multiple Parameters)
```csharp
// ✅ Use direct parameters when reasonable (2-3 params)
public interface IUpdateUserUseCase : IUseCase
{
    Task<User> Execute(string userId, string email, string name);
}

// ✅ Use record for 4+ parameters or when grouping makes sense
public record UpdateUserRequest
{
    public string UserId { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? PhoneNumber { get; init; }
}

public interface IUpdateUserUseCase : IUseCase
{
    Task<User> Execute(UpdateUserRequest request);
}

[UseCase]
public class UpdateUserUseCase(IUserRepository repository) : IUpdateUserUseCase
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public async Task<User> Execute(UpdateUserRequest request)
    {
        // Implementation
    }
}
```

### Response Objects - Only When Necessary
```csharp
// ✅ Return entity directly when possible
Task<User> Execute(string userId);

// ✅ Return simple type when appropriate
Task<bool> Execute(string userId);

// ✅ Use response record only when returning multiple pieces of data
public record SubscriptionValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

// ❌ Avoid useless wrappers
public record GetUserResult { public User User { get; init; } }  // Just return User!
```

## Repository Pattern

```csharp
// Filter
public class MyEntityFilter
{
    public List<long>? Ids { get; set; }
    public List<string>? UserIds { get; set; }
}

// Edit
public record MyEntityEdit
{
    public string UserId { get; init; } = null!;
    public string Name { get; init; } = null!;
}

// Entity
public record MyEntity : MyEntityEdit, IIdEntity
{
    public long Id { get; set; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset Updated { get; init; }
}
```

## Logging with NLog

```csharp
private static readonly Logger Log = LogManager.GetCurrentClassLogger();

// Debug - Detailed information
Log.Debug("Processing request with params: {param1}, {param2}", param1, param2);

// Info - Important business events
Log.Info("User {userId} completed action: {action}", userId, action);

// Warn - Recoverable issues
Log.Warn("API rate limit approached for user {userId}", userId);

// Error - Exceptions and failures
Log.Error(ex, "Failed to process for user {userId}", userId);
```

### Log Level Guidelines
- **Debug**: Detailed flow, parameters, internal state
- **Info**: Business events, operation completion
- **Warn**: Recoverable issues, degraded performance
- **Error**: Exceptions, failed operations

## Error Handling

### Use Case Layer
```csharp
// Throw business exceptions
if (entity == null)
    throw new NotFoundException($"Entity {id} not found");

if (!entity.IsActive)
    throw new InvalidOperationException("Entity not active");
```

### Controller Layer
```csharp
try
{
    var result = await useCase.Execute(request);
    return Ok(result);
}
catch (NotFoundException ex) { return NotFound(new { error = ex.Message }); }
catch (UnauthorizedAccessException) { return Unauthorized(); }
catch (Exception ex)
{
    Log.Error(ex, "Request failed");
    return StatusCode(500, new { error = "Internal server error" });
}
```

## Record Types

### Record Definition Style
```csharp
// ✅ Correct - Use { get; init; } syntax with object initializers
public record UserDto
{
    public long Id { get; init; }
    public string Email { get; init; } = null!;
}

// ✅ Correct - Initialize with object initializer
var dto = new UserDto
{
    Id = 123,
    Email = "user@example.com",
};

// ❌ Incorrect - Don't use positional record syntax for complex types
public record UserDto(long Id, string Email);  // Avoid for DTOs

// ✅ Use records for immutable data
public record UserDto
{
    public long Id { get; init; }
    public string Email { get; init; } = null!;
}

// ✅ Use records with inheritance
public record UserEdit
{
    public string Email { get; init; } = null!;
}

public record User : UserEdit, IIdEntity
{
    public long Id { get; set; }
    public DateTimeOffset Created { get; init; }
}

// Use `with` expressions for modifications
var updated = user with { Email = newEmail };
```

## Null Handling

### Use Nullable Reference Types
```csharp
// Required property
public string Email { get; init; } = null!;

// Optional property
public string? Description { get; init; }

// Null checking
if (string.IsNullOrEmpty(email))
    throw new ArgumentException("Email is required");

if (user?.IsActive == true)
    ProcessUser(user);
```

## Collection Initialization

### Use Collection Expressions (C# 12+)
```csharp
// ✅ Collection expressions
var list = [1, 2, 3];
var userIds = [user.Id];
var scopes = ["read", "write"];

// ✅ Empty collections
var empty = [];

// Traditional syntax still acceptable
var list = new List<int> { 1, 2, 3 };
```

## Async/Await Patterns

### Always Use await (Don't Return Task Directly)
```csharp
// ✅ Correct - Use await
public async Task<User> GetUser(long userId)
{
    var user = await repository.GetById(userId);
    return user;
}

// ❌ Avoid - Returning Task directly (unless intentional)
public Task<User> GetUser(long userId)
{
    return repository.GetById(userId);
}
```

### ConfigureAwait - Not Needed in ASP.NET Core
```csharp
// ✅ Correct - No ConfigureAwait needed
var result = await service.Process();

// ❌ Unnecessary in ASP.NET Core
var result = await service.Process().ConfigureAwait(false);
```

## Dependency Injection

### Constructor Injection with Primary Constructors
```csharp
[UseCase]
public class MyUseCase(
    IRepository repository,
    IService service,
    ILogger<MyUseCase> logger) : IMyUseCase
{
    // Dependencies available as parameters
}
```

### Service Registration
```csharp
// Use case - Automatic via [UseCase] attribute
[UseCase]
public class MyUseCase : IMyUseCase { }

// Service - Manual registration in module
services.AddScoped<IMyService, MyService>();
services.AddSingleton<ICacheService, CacheService>();
services.AddTransient<IEmailBuilder, EmailBuilder>();
```

## Date/Time Handling

### Always Use DateTimeOffset (Not DateTime)
```csharp
// ✅ Correct - DateTimeOffset for all timestamps
public DateTimeOffset Created { get; init; }
public DateTimeOffset? LastSync { get; init; }

// Get current time
var now = DateTimeOffset.UtcNow;

// ❌ Avoid DateTime
public DateTime Created { get; init; }  // Don't use
```

## Key Rules Summary

### Always Do
- ✅ Use primary constructors
- ✅ Explicit types in `new` expressions
- ✅ Trailing commas
- ✅ Clean names (no underscore prefix)
- ✅ Clean method names (no Async suffix)
- ✅ Use `record` with `{ get; init; }` syntax
- ✅ Use object initializers `{ ... }` for records
- ✅ Use `DateTimeOffset` for timestamps
- ✅ Use `await` in async methods
- ✅ Structured logging with NLog
- ✅ Throw business exceptions in use cases
- ✅ Use nullable reference types
- ✅ Avoid useless wrappers on use case inputs/outputs

### Never Do
- ❌ Traditional constructors
- ❌ `new()` without explicit type
- ❌ Underscore prefixes (`_field`)
- ❌ "Async" method suffixes
- ❌ DateTime (use DateTimeOffset)
- ❌ Returning Task without await (unless intentional)
- ❌ ConfigureAwait in ASP.NET Core
- ❌ Magic strings (use constants)
- ❌ Commented-out code in commits
- ❌ Positional record syntax for DTOs (use `{ get; init; }`)
- ❌ Constructor parameters on records (use object initializers)
- ❌ Wrapper records for single values (use direct parameters)
