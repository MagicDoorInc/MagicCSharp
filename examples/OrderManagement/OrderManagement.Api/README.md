# Order Management API - MagicCSharp Example

This is a complete example demonstrating **MagicCSharp best practices** for building enterprise-grade applications.

## What This Example Demonstrates

### ✅ Three-Layer Architecture

**Controllers** (HTTP Concerns)

- Extract parameters from HTTP requests
- Map DTOs to Request objects
- Map Results to DTOs
- Return appropriate HTTP status codes
- No business logic

**Use Cases** (Business Logic)

- Pure business workflows
- Coordinate repositories and services
- Dispatch events
- Use case chaining (composing complex workflows)
- Single responsibility

**Services** (Technical Implementation)

- External API calls (payment processing simulation)
- Technical operations
- Protocol implementations

### ✅ Use Case Pattern

- **Request/Result Pattern** - Clear contracts with `CreateOrderRequest` / `CreateOrderResult`
- **Automatic Registration** - `[UseCase]` attribute handles DI registration
- **Use Case Chaining** - `ProcessCompleteOrderUseCase` composes other use cases
- **Testability** - Pure constructor injection, no framework coupling

### ✅ Event-Driven Architecture

- **Event Dispatching** - Use cases dispatch events (`OrderCreated`, `PaymentProcessed`)
- **Event Handlers** - Separate handlers process events independently
- **Async Processing** - Handlers don't block the caller
- **Event Chaining** - Events can trigger more events
- **Local Implementation** - Uses `LocalEventDispatcher` (easy to swap for Kafka/SQS)

### ✅ Data Access

- **Repository Pattern** - `IOrderRepository` abstracts data access
- **Snowflake IDs** - Globally unique, time-sortable IDs
- **In-Memory Implementation** - Simple for examples (swap for real DB in production)

### ✅ Testable Infrastructure

- **IClock** - Time abstraction for testable time-dependent logic
- **IKeyGenService** - ID generation abstraction

## Project Structure

```
OrderManagement.Api/
├── Controllers/
│   └── OrdersController.cs          # HTTP concerns only
├── UseCases/
│   ├── CreateOrderUseCase.cs        # Create order + dispatch event
│   ├── ProcessPaymentUseCase.cs     # Process payment logic
│   ├── ProcessCompleteOrderUseCase.cs  # Chains use cases
│   └── OrderEventHandlers.cs        # Event handlers (also use cases!)
├── Domain/
│   ├── Entities/
│   │   └── Order.cs                 # Domain entities
│   └── Events/
│       └── OrderEvents.cs           # Event definitions
├── Data/
│   └── Repositories/
│       ├── IOrderRepository.cs      # Repository interface
│       └── InMemoryOrderRepository.cs  # In-memory implementation
├── DTOs/
│   └── OrderDTOs.cs                 # HTTP request/response objects
└── Program.cs                       # DI setup
```

## Running the Example

### 1. Build and Run

From the example directory:

```bash
dotnet restore
dotnet run
```

### 2. Visit Swagger UI

Open your browser to: `https://localhost:5001/swagger`

### 3. Try the APIs

**Create an Order:**

```bash
POST /api/orders
{
  "userId": 1,
  "items": [
    {
      "productId": 100,
      "productName": "Widget",
      "quantity": 2,
      "price": 29.99
    }
  ]
}
```

**Process Payment:**

```bash
POST /api/orders/{orderId}/payment
{
  "paymentMethod": "credit_card"
}
```

**Create and Process in One Request (Use Case Chaining):**

```bash
POST /api/orders/process
{
  "userId": 1,
  "items": [
    {
      "productId": 100,
      "productName": "Widget",
      "quantity": 2,
      "price": 29.99
    }
  ],
  "paymentMethod": "credit_card"
}
```

## Key Patterns Demonstrated

### 1. Use Case Pattern

```csharp
// Request - Input
public record CreateOrderRequest
{
    public long UserId { get; init; }
    public List<CreateOrderItem> Items { get; init; } = new();
}

// Result - Output
public record CreateOrderResult
{
    public long OrderId { get; init; }
    public decimal Total { get; init; }
}

// Use Case - Pure business logic
[UseCase]
public class CreateOrderUseCase(
    IOrderRepository orderRepository,
    IEventDispatcher events,
    IClock clock) : ICreateOrderUseCase
{
    public async Task<CreateOrderResult> Execute(CreateOrderRequest request)
    {
        // Business logic here
    }
}
```

### 2. Use Case Chaining

```csharp
[UseCase]
public class ProcessCompleteOrderUseCase(
    ICreateOrderUseCase createOrder,
    IProcessPaymentUseCase processPayment) : IProcessCompleteOrderUseCase
{
    public async Task Execute(ProcessCompleteOrderRequest request)
    {
        // Step 1: Create order
        var orderResult = await createOrder.Execute(...);

        // Step 2: Process payment
        var paymentResult = await processPayment.Execute(...);
    }
}
```

### 3. Event-Driven Workflow

```csharp
// Use case dispatches event
await events.Dispatch(new OrderCreated { OrderId = order.Id });

// Handler processes event independently
[UseCase]
public class LogOrderCreatedHandler : IEventHandler<OrderCreated>
{
    public Task Handle(OrderCreated evt)
    {
        // Handle event async - doesn't block the use case
    }
}
```

### 4. Controller Separation

```csharp
[ApiController]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromServices] ICreateOrderUseCase useCase,
        [FromBody] CreateOrderDto dto)
    {
        // 1. Map DTO → Request (HTTP concern)
        var request = new CreateOrderRequest { ... };

        // 2. Execute use case (business logic)
        var result = await useCase.Execute(request);

        // 3. Map Result → DTO and return HTTP response
        return CreatedAtAction(...);
    }
}
```

## Event Flow Example

When you create and process an order, watch the logs to see the event flow:

1. **POST /api/orders/process**
2. `CreateOrderUseCase` executes → creates order
3. `OrderCreated` event dispatched
4. `LogOrderCreatedHandler` handles event → logs creation
5. `ProcessPaymentUseCase` executes → processes payment
6. `PaymentProcessed` event dispatched
7. `ConfirmOrderOnPaymentHandler` handles event → confirms order
8. `OrderConfirmed` event dispatched
9. `SendOrderConfirmationHandler` handles event → sends confirmation

**All happening asynchronously and independently!**

## Scaling to Production

To scale this example to production:

### Swap Event Dispatcher

```csharp
// Development
services.RegisterLocalMagicEvents();

// Production
services.RegisterMagicKafkaEvents(kafkaConfig);
// OR
services.RegisterMagicSQSEvents(sqsConfig);

// Your code stays the same!
```

### Swap Repository

```csharp
// Development
services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

// Production
services.AddScoped<IOrderRepository, SqlOrderRepository>();
// OR
services.AddScoped<IOrderRepository, MongoOrderRepository>();
```

### Configure Snowflake IDs

```csharp
// Development
services.RegisterSnowflakeKeyGen(); // Random ID

// Production (multiple instances)
services.RegisterSnowflakeKeyGen(generatorId: instanceId);
```

### Add Distributed Locking

For background services:

```csharp
builder.Services.AddSingleton<IDistributedLockProvider>(...);
```

## Testing

Use cases are trivial to test:

```csharp
[Fact]
public async Task CreateOrder_ShouldGenerateSnowflakeId()
{
    // Arrange
    var fakeRepo = new FakeOrderRepository();
    var fakeEvents = new FakeEventDispatcher();
    var fakeClock = new FakeClock(new DateTime(2024, 1, 1));

    var useCase = new CreateOrderUseCase(fakeRepo, fakeEvents, fakeClock);

    // Act
    var result = await useCase.Execute(new CreateOrderRequest
    {
        UserId = 1,
        Items = new() { ... }
    });

    // Assert
    Assert.NotEqual(0, result.OrderId);
    Assert.True(fakeEvents.WasDispatched<OrderCreated>());
}
```

## Learn More

- [MagicCSharp Documentation](../../src/MagicCSharp/README.md)
- [MagicCSharp.Data Documentation](../../src/MagicCSharp.Data/README.md)
- [MagicCSharp.Events Documentation](../../src/MagicCSharp.Events/README.md)

## Key Takeaways

1. **Controllers handle HTTP** - DTOs, status codes, routing
2. **Use Cases handle business logic** - Pure workflows, testable
3. **Events decouple your system** - Async, independent handlers
4. **Use case chaining** - Build complex workflows from simple pieces
5. **Swap implementations easily** - Local to Kafka with zero code changes
6. **Everything is testable** - Constructor injection, no framework coupling

This is how you build enterprise-grade C# applications with MagicCSharp.
