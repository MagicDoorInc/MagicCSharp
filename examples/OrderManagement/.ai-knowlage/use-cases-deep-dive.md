# Use Cases - Quick Reference

## Core Pattern

Use cases implement business logic and orchestrate workflows. They coordinate services and repositories but contain NO HTTP or technical concerns.

### Interface Pattern
```csharp
public interface IUseCase { }  // Marker interface

public interface IMyUseCase : IUseCase
{
    Task<MyResult> Execute(MyRequest request);
}
```

### Implementation Pattern
```csharp
[UseCase]  // Auto-registers in DI
public class MyUseCase(
    IRepository repository,
    IExternalService service,
    IOtherUseCase otherUseCase) : IMyUseCase
{
    public async Task<MyResult> Execute(MyRequest request)
    {
        // Implement business logic here
    }
}
```

### Request/Response Pattern
```csharp
// Request - Input data
public record MyRequest
{
    public string UserId { get; init; } = null!;
    public long EntityId { get; init; }
}

// Result - Output data
public record MyResult
{
    public bool Success { get; init; }
    public string Data { get; init; } = null!;
}
```

## Automatic Registration

### UseCase Attribute
```csharp
[UseCase]                            // Scoped (default)
[UseCase(ServiceLifetime.Scoped)]   // Explicit scoped
[UseCase(ServiceLifetime.Singleton)] // For stateless use cases
[UseCase(ServiceLifetime.Transient)] // For transient use cases
```

### Registration in Program.cs
```csharp
builder.Services.AddUseCases(Assembly.GetExecutingAssembly());
```

## Use Case Patterns

### 1. Simple Use Case (Single Operation)
```csharp
[UseCase]
public class GetUserIntegrationsUseCase(
    IIntegrationProvidersRepository integrationRepository,
    IServiceConnectionsRepository serviceConnectionsRepository) : IGetUserIntegrationsUseCase
{
    public async Task<GetUserIntegrationsResult> Execute(GetUserIntegrationsRequest request)
    {
        var integrations = await integrationRepository.Get(new IntegrationProviderFilter
        {
            UserIds = [request.UserId],
        });

        var result = new List<IntegrationWithServices>();

        foreach (var integration in integrations)
        {
            var services = await serviceConnectionsRepository.Get(new ServiceConnectionFilter
            {
                IntegrationProviderIds = [integration.Id],
            });

            result.Add(new IntegrationWithServices
            {
                Integration = integration,
                Services = services,
            });
        }

        return new GetUserIntegrationsResult { Integrations = result };
    }
}
```

### 2. Complex Orchestration (Multi-Step Workflow)
```csharp
[UseCase]
public class CompleteGoogleOAuthUseCase(
    IGoogleOAuthService oauthService,
    IOAuthStateService stateService,
    IEncryptionService encryptionService,
    IIntegrationProvidersRepository integrationRepository,
    IProviderCredentialsRepository credentialsRepository,
    IServiceConnectionsRepository serviceConnectionsRepository,
    ISyncCalendarEventsUseCase syncCalendarEventsUseCase) : ICompleteGoogleOAuthUseCase
{
    public async Task<CompleteGoogleOAuthResult> Execute(CompleteGoogleOAuthRequest request)
    {
        // 1. Validate OAuth state (business rule)
        var stateRecord = await stateService.ValidateAndConsumeState(request.State);
        if (stateRecord == null)
            throw new UnauthorizedAccessException("Invalid OAuth state");

        // 2. Exchange code for tokens (technical via service)
        var tokens = await oauthService.ExchangeCodeForTokens(request.Code);
        var userInfo = await oauthService.GetUserInfo(tokens.AccessToken);

        // 3. Business logic - create or update integration
        var integration = await CreateOrUpdateIntegration(stateRecord.UserId, userInfo);

        // 4. Store encrypted credentials
        await StoreCredentials(integration.Id, tokens);

        // 5. Enable default services
        await EnableDefaultServices(integration.Id);

        // 6. Trigger initial sync (workflow)
        await syncCalendarEventsUseCase.Execute(new SyncCalendarEventsRequest
        {
            IntegrationId = integration.Id,
        });

        return new CompleteGoogleOAuthResult
        {
            IntegrationId = integration.Id,
            UserId = stateRecord.UserId,
        };
    }
}
```

### 3. Composition (Use Case Calls Other Use Cases)
```csharp
[UseCase]
public class ProcessMeetingPreparationUseCase(
    ICreateSummaryForMeetingUseCase createSummary,
    IRevocoAIClient aiClient,
    INotificationService notificationService) : IProcessMeetingPreparationUseCase
{
    public async Task Execute(ProcessMeetingPreparationRequest request)
    {
        // Call other use case
        var summary = await createSummary.Execute(request.CalendarEvent);

        // Apply business logic
        if (summary.EmailThreads.Any() || summary.Documents.Any())
        {
            var aiSummary = await GenerateAISummary(request.CalendarEvent, summary);
            summary = summary with { GeneratedSummary = aiSummary };
        }

        // Call service
        await notificationService.SendMeetingPreparation(request.CalendarEvent, summary);
    }
}
```

## Use Cases vs. Services

| Aspect | Use Case | Service |
|--------|----------|---------|
| **Purpose** | Business workflows | Technical operations |
| **Coordinates** | Services + Repositories + Use Cases | External APIs |
| **Makes decisions** | ✅ Yes (business rules) | ❌ No (pure implementation) |
| **Throws** | Business exceptions (NotFoundException) | Technical exceptions (HttpRequestException) |
| **Returns** | Business results | Technical data |
| **Example** | "Complete OAuth and setup services" | "Exchange code for tokens via HTTP" |

## Error Handling

### Business Validation
```csharp
public async Task<MyResult> Execute(MyRequest request)
{
    // Validate business rules
    var entity = await repository.Get(request.Id);
    if (entity == null)
        throw new NotFoundException($"Entity {request.Id} not found");

    if (entity.UserId != request.UserId)
        throw new UnauthorizedAccessException("User cannot access this entity");

    if (entity.Status != EntityStatus.Active)
        throw new InvalidOperationException("Entity is not in active status");

    // Continue with business logic...
}
```

### Service Coordination Error Handling
```csharp
public async Task<MyResult> Execute(MyRequest request)
{
    try
    {
        var result = await primaryService.Process(request);
        return new MyResult { Success = true, Data = result };
    }
    catch (ExternalServiceException ex)
    {
        Log.Warn(ex, "Primary service failed, trying fallback");

        try
        {
            var fallbackResult = await fallbackService.Process(request);
            return new MyResult { Success = true, Data = fallbackResult, UsedFallback = true };
        }
        catch (Exception fallbackEx)
        {
            Log.Error(fallbackEx, "All services failed");
            throw new BusinessOperationException("All services unavailable", ex);
        }
    }
}
```

## Best Practices

### 1. Single Responsibility
```csharp
// ✅ Good - One clear purpose per use case
public class ConnectGoogleAccountUseCase { }
public class DisconnectGoogleAccountUseCase { }
public class RefreshGoogleTokensUseCase { }

// ❌ Bad - Multiple responsibilities
public class ManageGoogleAccountUseCase
{
    public Task Connect() { }
    public Task Disconnect() { }
    public Task Refresh() { }
}
```

### 2. Use Primary Constructors
```csharp
// ✅ Good - Primary constructor with DI
[UseCase]
public class MyUseCase(
    IRepository repository,
    IService service) : IMyUseCase
{
    // Use injected dependencies directly
}
```

### 3. Strongly-Typed Request/Response
```csharp
// ✅ Good - Strongly typed
public record ProcessPaymentRequest
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public string PaymentMethodId { get; init; } = null!;
}

// ❌ Bad - Primitive parameters
public Task<Result> Execute(decimal amount, string currency, string paymentMethodId)
```

### 4. Log Business Operations
```csharp
public async Task<MyResult> Execute(MyRequest request)
{
    Log.Info("Starting operation for user: {userId}", request.UserId);

    try
    {
        var result = await ProcessOperation(request);
        Log.Info("Operation completed for user: {userId}", request.UserId);
        return result;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Operation failed for user: {userId}", request.UserId);
        throw;
    }
}
```

### 5. Input and Business Validation
```csharp
public async Task<MyResult> Execute(MyRequest request)
{
    // Input validation
    if (request.Amount <= 0)
        throw new ArgumentException("Amount must be positive");

    // Business rule validation
    var account = await accountRepository.Get(request.AccountId);
    if (account == null)
        throw new NotFoundException($"Account {request.AccountId} not found");

    if (account.Balance < request.Amount)
        throw new InvalidOperationException("Insufficient balance");

    // Continue with business logic...
}
```

## Common Patterns Checklist

When creating a use case, ensure:

- [ ] Implements `IUseCase` marker interface
- [ ] Has specific interface `IMyUseCase : IUseCase`
- [ ] Decorated with `[UseCase]` attribute
- [ ] Uses primary constructor for DI
- [ ] Has strongly-typed Request and Result records
- [ ] Contains ONLY business logic (no HTTP, no technical protocols)
- [ ] Throws business exceptions (NotFoundException, InvalidOperationException)
- [ ] Logs key business operations
- [ ] Validates business rules
- [ ] Coordinates services/repositories/other use cases
- [ ] Returns business results (not HTTP responses)
- [ ] Named with clear, single-purpose name (verb + noun pattern)

## Key Rules

- ✅ **Do**: Business workflows, rule validation, service coordination
- ✅ **Do**: Throw business exceptions (NotFoundException, InvalidOperationException)
- ✅ **Do**: Use Request/Response records
- ✅ **Do**: Coordinate multiple services and use cases
- ❌ **Never**: Return HTTP status codes (IActionResult, ActionResult)
- ❌ **Never**: Handle HTTP concerns (headers, cookies, status codes)
- ❌ **Never**: Implement technical protocols (OAuth flows, encryption)
- ❌ **Never**: Work with DTOs (use entities)
