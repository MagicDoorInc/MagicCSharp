# MagicCSharp.Events

**Event-driven architecture made simple**

Build decoupled, scalable applications with a clean event-driven architecture. MagicCSharp.Events handles event
dispatching, serialization, and handler execution with priority ordering and comprehensive monitoring.

## Registration order

Two calls, and the first is not optional:

```csharp
services.RegisterMagicEvents();        // handler discovery, serializer, IAsyncEventDispatcher
services.RegisterLocalMagicEvents();   // then a transport — or RegisterMagicKafkaEvents / RegisterMagicSQSEvents
```

`RegisterMagicEvents` is what finds your `IEventHandler<T>` implementations and registers
`IAsyncEventDispatcher`. The transport call only registers `IEventDispatcher`, so calling it alone leaves
the application failing at resolution the first time anything dispatches. `MagicCSharp.App` does both for
you.

**`Priority` must be `static`.** The dispatcher reads it without constructing the handler, so an instance
property compiles and is silently ignored — your handler runs at the default priority. The interface
declares it `static virtual`.

```csharp
public class SendOrderConfirmationHandler : IEventHandler<OrderCreated>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public Task Handle(OrderCreated evt) => ...;
}
```

## Why MagicCSharp.Events?

✅ **Priority-Based Execution** - Control handler execution order

✅ **Automatic Registration** - Event handlers register themselves

✅ **Type-Safe Events** - Full IntelliSense support

✅ **OpenTelemetry Metrics** - Built-in performance monitoring

✅ **Flexible Dispatching** - Synchronous and asynchronous dispatching

✅ **Distributed Events** - Kafka and AWS SQS support available

## Installation

```bash
dotnet add package MagicCSharp.Events
dotnet add package MagicCSharp
```

## Quick Start

### 1. Define an Event

```csharp
public record UserCreatedEvent : MagicEvent
{
    public long UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}
```

### 2. Create Event Handlers

```csharp
public class SendWelcomeEmailHandler(IEmailService emailService)
    : IEventHandler<UserCreatedEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public async Task Handle(UserCreatedEvent @event)
    {
        await emailService.SendWelcomeEmail(@event.Email, @event.Name);
    }
}

public class CreateUserProfileHandler(IProfileRepository profileRepository)
    : IEventHandler<UserCreatedEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.AddDataNoDependencies;

    public async Task Handle(UserCreatedEvent @event)
    {
        await profileRepository.Create(new Profile
        {
            UserId = @event.UserId,
            Email = @event.Email
        });
    }
}
```

### 3. Register Events

```csharp
// In your Startup.cs or Program.cs
services.RegisterMagicEvents();
// Register the LocalEventHandler as the IEventDispatcher
services.RegisterLocalMagicEvents();

// Optional: Enable OpenTelemetry metrics
// services.RegisterMagicEvents(useOpenTelemetryMetrics: true);
```

### 4. Dispatch Events

```csharp
public class UserService(IEventDispatcher eventDispatcher, IUserRepository userRepository)
{
    public async Task<User> CreateUser(CreateUserRequest request)
    {
        var user = await userRepository.Create(request);

        // Dispatch the event - all handlers will execute
        eventDispatcher.Dispatch(new UserCreatedEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name
        });

        return user;
    }
}
```

## Features

### 🎯 Priority-Based Handler Execution

Control the order in which handlers execute using priorities:

```csharp
public class CreateRelatedDataHandler : IEventHandler<OrderCreatedEvent>
{
    // Executes first - no dependencies
    public static MagicEventPriority Priority => MagicEventPriority.AddDataNoDependencies;

    public async Task Handle(OrderCreatedEvent @event)
    {
        await CreateOrderItems(@event.OrderId);
    }
}

public class UpdateInventoryHandler : IEventHandler<OrderCreatedEvent>
{
    // Executes second - depends on order items existing
    public static MagicEventPriority Priority => MagicEventPriority.AddDataWithDependencies;

    public async Task Handle(OrderCreatedEvent @event)
    {
        await UpdateInventoryLevels(@event.OrderId);
    }
}

public class SendConfirmationHandler : IEventHandler<OrderCreatedEvent>
{
    // Executes last - notify user after everything is done
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public async Task Handle(OrderCreatedEvent @event)
    {
        await SendOrderConfirmation(@event.OrderId);
    }
}
```

**Built-in Priorities:**

- `Cron = -1` - For scheduled/cron tasks
- `AddDataNoDependencies = 0` - Create data with no dependencies
- `AddDataWithDependencies = 1000` - Create data that depends on other handlers
- `UpdateMetadata = 2000` - Update metadata after data is created
- `DeleteData = 2500` - Delete operations
- `NotifyUser = 3000` - Send notifications
- `RunLast = 10000` - Anything that should run last

**Important:** All handlers for a given event are always executed on the same server instance. This ensures that the
priority-based execution order is maintained and allows you to chain operations where certain tasks must complete before
others begin. This is particularly useful when handlers have dependencies on each other's side effects.

### 🏠 Always Use IEventDispatcher

**Important:** Always inject and use `IEventDispatcher` in your application code, never use specific implementations
directly. This allows you to switch between local and distributed event processing without changing your business logic.

```csharp
// ✅ CORRECT - Always use IEventDispatcher
public class OrderService(IEventDispatcher eventDispatcher)
{
    public void CreateOrder(CreateOrderRequest request)
    {
        var order = SaveOrder(request);
        eventDispatcher.Dispatch(new OrderCreatedEvent { OrderId = order.Id });
    }
}

// ❌ WRONG - Don't use specific implementations
public class OrderService(LocalEventDispatcher dispatcher) // Don't do this!
{
    // ...
}
```

**Local Development**

Use `RegisterLocalMagicEvents()` to register `LocalEventDispatcher` as the implementation of `IEventDispatcher`. This
executes all handlers synchronously in the same process:

```csharp
// In your Startup.cs or Program.cs
services.RegisterLocalMagicEvents();

// This registers:
// - IEventDispatcher → LocalEventDispatcher (executes handlers in-process)
// - IAsyncEventDispatcher → AsyncEventDispatcher (async version)
// - Event serialization, metrics, and handler discovery
```

**What happens:**

```csharp
public class OrderService(IEventDispatcher eventDispatcher)
{
    public void CreateOrder(CreateOrderRequest request)
    {
        var order = SaveOrder(request);

        // Executes all handlers immediately in this process
        // Blocks until all handlers complete
        eventDispatcher.Dispatch(new OrderCreatedEvent { OrderId = order.Id });

        // All handlers have finished executing at this point
    }
}
```

**Perfect for:**

- Local development and testing
- Single-service applications
- When you need immediate execution
- Unit and integration testing without infrastructure

**Distributed Events (Kafka/SQS)**

For distributed systems, use `RegisterMagicKafkaEvents()` or `RegisterMagicSQSEvents()`. These register the distributed
event dispatcher as `IEventDispatcher`:

```csharp
// In your Startup.cs or Program.cs

// Register Kafka
services.RegisterMagicKafkaEvents(kafkaConfig);
// OR
// Register SQS
services.RegisterMagicSQSEvents(sqsConfig);

// This registers:
// - IEventDispatcher → KafkaEventDispatcher (or SqsEventDispatcher)
// - IAsyncEventDispatcher → AsyncEventDispatcher (for consuming events)
// - Event serialization, metrics, and handler discovery
// - Background service to consume events from Kafka/SQS
```

**What happens:**

```csharp
public class OrderService(IEventDispatcher eventDispatcher)
{
    public void CreateOrder(CreateOrderRequest request)
    {
        var order = SaveOrder(request);

        // Queues event to Kafka/SQS, returns immediately
        eventDispatcher.Dispatch(new OrderCreatedEvent { OrderId = order.Id });

        // Handler execution happens asynchronously on consumer services
    }
}
```

**Perfect for:**

- Microservices architectures
- Cross-service communication
- Async, fire-and-forget event processing
- Resilient, distributed systems

**Key Benefits:**

- **Zero Code Changes** - Same `IEventDispatcher` interface for both local and distributed
- **Easy Testing** - Test locally without Kafka/SQS infrastructure
- **Flexible Deployment** - Switch from local to distributed by changing registration only

### 📊 OpenTelemetry Metrics

Track event processing with built-in metrics:

```csharp
services.RegisterMagicEvents(useOpenTelemetryMetrics: true);
```

**Metrics Collected:**

- `Events` - Counter of events received by type
- `Events.Failed` - Counter of failed events by type and handler
- `Events.Finished` - Counter of completed events by type and handler
- `Events.ExecutionTime` - Histogram of execution times by type and handler

**View in your monitoring system:**

```
Events{eventType="UserCreatedEvent"} = 1234
Events.Failed{eventType="OrderCreatedEvent", handlerName="SendEmailHandler"} = 5
Events.ExecutionTime{eventType="OrderCreatedEvent", handlerName="UpdateInventoryHandler"} = 125ms
```

### ✅ Type-Safe Event Serialization

Events are automatically serialized with type information:

```csharp
{
  "type": "UserCreatedEvent",
  "body": {
    "userId": "123",
    "email": "user@example.com",
    "name": "John Doe",
    "eventId": "abc-123",
    "occurredOn": "2024-01-15T10:30:00Z"
  }
}
```

**Features:**

- Handles polymorphic deserialization
- Converts longs to strings (prevents JavaScript precision loss)
- Skips unknown event types gracefully
- Case-insensitive property names

### 🧪 Testing Events

Testing event handlers is straightforward:

```csharp
[Fact]
public async Task SendWelcomeEmailHandler_SendsEmail()
{
    // Arrange
    var emailService = new Mock<IEmailService>();
    var handler = new SendWelcomeEmailHandler(emailService.Object);

    var @event = new UserCreatedEvent
    {
        UserId = 123,
        Email = "test@example.com",
        Name = "Test User"
    };

    // Act
    await handler.Handle(@event);

    // Assert
    emailService.Verify(x =>
        x.SendWelcomeEmail("test@example.com", "Test User"),
        Times.Once);
}
```

**Testing with IEventDispatcher:**

```csharp
[Fact]
public void CreateUser_DispatchesEvent()
{
    // Arrange
    var services = new ServiceCollection();
    services.RegisterLocalMagicEvents();
    services.AddTransient<IEventHandler<UserCreatedEvent>, SendWelcomeEmailHandler>();
    // ... register other dependencies

    var serviceProvider = services.BuildServiceProvider();
    var dispatcher = serviceProvider.GetRequiredService<IEventDispatcher>();

    // Act
    dispatcher.Dispatch(new UserCreatedEvent { ... });

    // Assert - verify handlers executed
}
```

### 🔧 Custom Event Handlers

Implement `IEventHandler<T>` to handle any event:

```csharp
public class AuditLogHandler(IAuditRepository auditRepository)
    : IEventHandler<UserCreatedEvent>,
      IEventHandler<UserUpdatedEvent>,
      IEventHandler<UserDeletedEvent>
{
    public async Task Handle(UserCreatedEvent @event)
    {
        await auditRepository.Log("User created", @event.UserId);
    }

    public async Task Handle(UserUpdatedEvent @event)
    {
        await auditRepository.Log("User updated", @event.UserId);
    }

    public async Task Handle(UserDeletedEvent @event)
    {
        await auditRepository.Log("User deleted", @event.UserId);
    }
}
```

**Handler Lifetime:**
Event handlers are registered as **Transient** by default. They are created fresh for each event.

### 🎨 Advanced Patterns

**Conditional Handlers:**

```csharp
public class PremiumUserWelcomeHandler : IEventHandler<UserCreatedEvent>
{
    public async Task Handle(UserCreatedEvent @event)
    {
        if (!@event.IsPremium)
            return; // Skip for non-premium users

        await SendPremiumWelcomePackage(@event.UserId);
    }
}
```

**Batch Operations:**

```csharp
public class BatchNotificationHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly List<long> orderIds = new();

    public async Task Handle(OrderCreatedEvent @event)
    {
        orderIds.Add(@event.OrderId);

        if (orderIds.Count >= 100)
        {
            await SendBatchNotification(orderIds);
            orderIds.Clear();
        }
    }
}
```

## Complete Example

```csharp
// 1. Define Event
public record OrderCreatedEvent : MagicEvent
{
    public long OrderId { get; init; }
    public long CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
}

// 2. Create Handlers
public class UpdateInventoryHandler(IInventoryService inventoryService)
    : IEventHandler<OrderCreatedEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.AddDataWithDependencies;

    public async Task Handle(OrderCreatedEvent @event)
    {
        await inventoryService.ReserveInventory(@event.OrderId);
    }
}

public class SendConfirmationHandler(IEmailService emailService)
    : IEventHandler<OrderCreatedEvent>
{
    public static MagicEventPriority Priority => MagicEventPriority.NotifyUser;

    public async Task Handle(OrderCreatedEvent @event)
    {
        await emailService.SendOrderConfirmation(@event.OrderId);
    }
}

// 3. Register
services.RegisterLocalMagicEvents();

// 4. Dispatch
public class OrderService(IEventDispatcher eventDispatcher)
{
    public async Task CreateOrder(CreateOrderRequest request)
    {
        var order = await SaveOrder(request);

        eventDispatcher.Dispatch(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount
        });
    }
}
```

## Distributed Event Processing

For distributed event processing with Kafka or SQS:

**[MagicCSharp.Events.Kafka](https://www.nuget.org/packages/MagicCSharp.Events.Kafka)** - Kafka integration
**[MagicCSharp.Events.SQS](https://www.nuget.org/packages/MagicCSharp.Events.SQS)** - AWS SQS integration

## Related Packages

**[MagicCSharp](https://www.nuget.org/packages/MagicCSharp)** - Core infrastructure library (required)
**[MagicCSharp.Data](https://www.nuget.org/packages/MagicCSharp.Data)** - Repository pattern and data access

## License

MIT License - See LICENSE file for details.
