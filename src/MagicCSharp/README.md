# MagicCSharp

**Enterprise-grade infrastructure for clean, maintainable C# applications**

MagicCSharp provides the foundational building blocks for building scalable, maintainable, and testable enterprise applications using clean architecture principles. Stop writing boilerplate infrastructure code and focus on your business logic.

## Why MagicCSharp?

✅ **Use Case Pattern** - Organize business logic with clear separation of concerns

✅ **Automatic Service Registration** - Use cases register themselves automatically

✅ **Testable Time** - Mock time in your tests with ease

✅ **Distributed Locking** - Built-in support for distributed locks

✅ **Drift-Free Scheduling** - Background services that stay on schedule

✅ **Request Tracking** - Track requests across async boundaries

✅ **Production-Ready** - Battle-tested patterns from real-world applications

## Installation

```bash
dotnet add package MagicCSharp
```

## Three-Layer Architecture

MagicCSharp enforces clean separation of concerns with a three-layer architecture:

| Layer | Responsibility | HTTP Concerns | Business Logic | Technical Implementation |
|-------|---------------|---------------|----------------|-------------------------|
| **Controllers** | API routing & DTOs | ✅ Yes | ❌ No | ❌ No |
| **Use Cases** | Business workflows | ❌ No | ✅ Yes | ❌ No |
| **Services** | External APIs & protocols | ❌ No | ❌ No | ✅ Yes |

### Data Flow

```
HTTP Request
    ↓
Controller (HTTP concerns)
    ├── Extract parameters from HTTP
    ├── Validate authentication
    ├── Map DTO → Request
    ↓
Use Case (Business logic)
    ├── Validate business rules
    ├── Call Services (technical operations)
    ├── Call Repositories (data access)
    ├── Call Other Use Cases (workflows)
    ├── Return business result
    ↓
Controller (HTTP concerns)
    ├── Map Result → DTO
    ├── Return HTTP status + response
    ↓
HTTP Response
```

## Quick Start

### 1. Define a Use Case

```csharp
using MagicCSharp.UseCases;

// Request - Input data
public record CreateUserRequest
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

// Result - Output data
public record CreateUserResult
{
    public long UserId { get; init; }
    public bool Success { get; init; }
}

// Interface
public interface ICreateUserUseCase : IUseCase
{
    Task<CreateUserResult> Execute(CreateUserRequest request);
}

// Implementation
[MagicUseCase]
public class CreateUserUseCase(
    IUserRepository userRepository,
    IEmailService emailService) : ICreateUserUseCase
{
    public async Task<CreateUserResult> Execute(CreateUserRequest request)
    {
        // Business logic only - no HTTP concerns
        var user = await userRepository.Create(new UserEdit
        {
            Email = request.Email,
            Name = request.Name
        });

        // Coordinate services
        await emailService.SendWelcomeEmail(user.Email);

        return new CreateUserResult
        {
            UserId = user.Id,
            Success = true
        };
    }
}
```

### 2. Register Use Cases

```csharp
// In your Startup.cs or Program.cs
services.AddMagicUseCases();
```

**Important for Multi-Assembly Projects:**

If your use case interfaces are defined in separate C# libraries, you must ensure those assemblies are loaded before calling `AddMagicUseCases()`. C# loads assemblies JIT (Just-In-Time) - the .dll won't be loaded into the domain until code that references it is executed.

```csharp
// Force assembly loading by referencing a type from each library
_ = typeof(MyLibrary1.ISomeUseCase);
_ = typeof(MyLibrary2.IAnotherUseCase);

// Now register use cases - all assemblies are loaded
services.AddMagicUseCases();
```

Without forcing the assembly to load first, use cases in other libraries won't be discovered during registration, even if you use them later in your application.

### 3. Use in Controller

```csharp
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(
        [FromServices] ICreateUserUseCase useCase,
        [FromBody] CreateUserDto dto)
    {
        try
        {
            // Controller handles HTTP concerns only
            var result = await useCase.Execute(new CreateUserRequest
            {
                Email = dto.Email,
                Name = dto.Name
            });

            return Ok(new UserDto { Id = result.UserId });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
```

That's it! Your use case is automatically registered and follows clean architecture principles.

## Features

### 🎯 Use Case Pattern

The use case pattern helps organize business logic into small, focused classes that are easy to test and maintain.

```csharp
// Use cases implement IMagicUseCase
public class SendWelcomeEmailUseCase(IEmailService emailService) : IMagicUseCase
{
    public async Task Execute(string email)
    {
        await emailService.SendWelcomeEmail(email);
    }
}

// Control the lifetime with an attribute
[MagicUseCase(ServiceLifetime.Singleton)]
public class CacheWarmerUseCase : IMagicUseCase
{
    // This use case will be registered as a singleton
}
```

**Automatic Registration** - All classes implementing `IMagicUseCase` are automatically registered in your DI container.

**Lifetime Control** - Use `[MagicUseCase(ServiceLifetime)]` to control service lifetime (Scoped by default).

### ⏰ Testable Time

Stop using `DateTime.Now` and start using `IClock` for time that you can control in tests.

```csharp
public class OrderService(IClock clock)
{
    public Order CreateOrder()
    {
        return new Order
        {
            CreatedAt = clock.Now(),
            ExpiresAt = clock.Now().AddDays(30)
        };
    }
}

// In production
services.AddSingleton<IClock, DateTimeClock>();

// In tests
var fakeClock = new FakeClock(new DateTime(2024, 1, 1));
var service = new OrderService(fakeClock);
```

### 🔒 Distributed Locking

Prevent concurrent execution across multiple instances using distributed locks powered by [Medallion.Threading](https://github.com/madelson/DistributedLock).

```csharp
public class ScheduledReportGenerator(
    IDistributedLockProvider lockProvider) : ScheduledBackgroundService(...)
{
    protected override string LockName => "report-generator";

    protected override async Task ExecuteScheduledAsync(CancellationToken cancellationToken)
    {
        // Only one instance will execute at a time
        await GenerateReports();
    }
}
```

**Supports Multiple Backends:**
- File-based locks (default, good for single-server)
- Redis locks (for distributed systems)
- SQL Server locks
- Azure Blob Storage
- And more via Medallion.Threading

### 📅 Scheduled Background Services

Run tasks on a schedule without drift using `ScheduledBackgroundService`.

```csharp
public class DailyReportService(
    IServiceScopeFactory serviceScopeFactory,
    IClock clock,
    ILogger<DailyReportService> logger)
    : ScheduledBackgroundService(
        serviceScopeFactory,
        new TimeOfDaySchedule(new TimeOnly(2, 0)), // Run at 2 AM every day
        lockProvider: null, // Optional distributed lock
        clock: clock,
        logger: logger)
{
    protected override async Task ExecuteScheduledAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var reportGenerator = serviceProvider.GetRequiredService<IReportGenerator>();
        await reportGenerator.GenerateDailyReport();
    }
}

// Register as a hosted service
services.AddHostedService<DailyReportService>();
```

**Two Schedule Types:**

**Time of Day Schedule** - Run at specific times:
```csharp
new TimeOfDaySchedule(new TimeOnly(2, 0))  // 2 AM daily
```

**Interval Schedule** - Run at regular intervals:
```csharp
new IntervalSchedule(TimeSpan.FromMinutes(5))  // Every 5 minutes
```

**Drift-Free Execution** - Uses anchor points to prevent schedule drift over time.

### 🔍 Request ID Tracking

Track requests across async boundaries and correlated logs with automatic middleware.

**Setup (recommended):**

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register RequestIdHandler
builder.Services.AddSingleton<IRequestIdHandler, RequestIdHandler>();

var app = builder.Build();

// Add RequestId middleware - should be early in the pipeline
app.UseRequestId();

app.Run();
```

The middleware automatically:
- Accepts `X-Request-ID` header from clients (for distributed tracing)
- Generates an 8-character unique ID if no header is provided
- Adds the RequestId to response headers for client tracking
- Makes the RequestId available throughout the entire request lifecycle

**Accessing RequestId:**

```csharp
public class MyService(IRequestIdHandler requestIdHandler)
{
    public async Task ProcessData()
    {
        // RequestId is automatically available - set by middleware
        var currentId = requestIdHandler.GetCurrentRequestId();

        // Use in logging, tracing, etc.
        _logger.LogInformation("Processing request {RequestId}", currentId);

        await SomeAsyncOperation(); // RequestId flows through all async calls
    }
}
```

**Advanced: Manual RequestId Management:**

For background jobs, event handlers, or child operations:

```csharp
// Set a specific RequestId (e.g., from event metadata)
using (requestIdHandler.SetRequestId("abc12345"))
{
    await ProcessEvent();
}

// Generate a new RequestId manually
using (requestIdHandler.SetRequestId())
{
    await BackgroundJob();
}

// Create child RequestId for sub-operations
using (requestIdHandler.SetChildRequestId())
{
    // Child ID format: {parent}-{8-char-guid}
    // Example: abc12345-def67890
    await ProcessSubTask();
}
```

**NLog Integration:**

Configure NLog to automatically include RequestId in all logs:

```xml
<!-- nlog.config -->
<nlog>
  <extensions>
    <add assembly="NLog.Web.AspNetCore"/>
  </extensions>

  <targets>
    <target name="jsonfile" xsi:type="File" fileName="logs/app.json">
      <layout xsi:type="JsonLayout">
        <attribute name="timestamp" layout="${longdate}" />
        <attribute name="level" layout="${level:upperCase=true}"/>
        <attribute name="logger" layout="${logger}"/>
        <attribute name="message" layout="${message}" />
        <attribute name="requestId" layout="${aspnet-item:variable=RequestId:whenEmpty=System}"/>
        <attribute name="exception" layout="${exception:format=toString}"/>
      </layout>
    </target>
  </targets>

  <rules>
    <logger name="*" minlevel="Info" writeTo="jsonfile" />
  </rules>
</nlog>
```

```csharp
// In your service, set RequestId in ASP.NET context for NLog
public class MyService(IRequestIdHandler requestIdHandler, IHttpContextAccessor httpContextAccessor)
{
    public void LogWithRequestId()
    {
        var requestId = requestIdHandler.GetCurrentRequestId();
        httpContextAccessor.HttpContext?.Items["RequestId"] = requestId;

        _logger.LogInformation("This log will include the RequestId");
    }
}
```

## Exception Handling

MagicCSharp provides base exception types for common scenarios:

```csharp
// Throw when an entity is not found
throw new NotFoundIdException<User>(userId);
throw new NotFoundKeyException<Product>(productKey);

// Helper methods
var user = await userRepository.Get(id);
NotFoundException.ThrowIfNull(user, id);
```

## Related Packages

**[MagicCSharp.Data](https://www.nuget.org/packages/MagicCSharp.Data)** - Repository pattern and data access
**[MagicCSharp.Events](https://www.nuget.org/packages/MagicCSharp.Events)** - Event-driven architecture

## License

MIT License - See LICENSE file for details.
