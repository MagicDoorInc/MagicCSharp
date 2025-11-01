# MagicCSharp

**Stop writing infrastructure code. Start building features.**

MagicCSharp is a complete toolkit for building enterprise-grade, distributed C# applications with clean architecture patterns. Go from prototype to production-ready distributed systems without the boilerplate.

## Why MagicCSharp?

### Built for Scale from Day One

**Start local, scale globally** - Write your code once using clean patterns. Switch from in-memory to distributed infrastructure with zero code changes.

```csharp
// Local development
services.RegisterLocalMagicEvents();

// Production with Kafka
services.RegisterMagicKafkaEvents(kafkaConfig);

// Your code stays the same
eventDispatcher.Dispatch(new OrderCreated { OrderId = 123 });
```

### Clean Architecture That Scales

**Three-layer separation** keeps your business logic pure and testable:

- **Controllers** - Handle HTTP concerns, DTOs, authentication
- **Use Cases** - Pure business logic, orchestrate workflows
- **Services** - External APIs, protocols, technical implementations

No more mixing HTTP logic with business rules. No more untestable code.

### Production-Ready Infrastructure

**Everything you need for distributed systems:**

✅ **Distributed Events** - Kafka, SQS, or in-memory with the same interface

✅ **Distributed Locking** - Coordinate work across multiple instances

✅ **Drift-Free Scheduling** - Background jobs that stay on schedule

✅ **Snowflake IDs** - Globally unique, time-sortable IDs for distributed databases

✅ **Request Tracking** - Trace requests across async boundaries

✅ **Testable Time** - Mock time in tests with `IClock`

## Quick Start

### Installation

```bash
# Core framework
dotnet add package MagicCSharp

# Data access & repositories
dotnet add package MagicCSharp.Data

# Event-driven architecture
dotnet add package MagicCSharp.Events
dotnet add package MagicCSharp.Events.Kafka  # Optional
dotnet add package MagicCSharp.Events.SQS    # Optional
```

### Define a Use Case

**Use cases are just classes** - No base classes, no framework coupling, just pure C# with a marker interface.

```csharp
// Request/Result pattern keeps contracts clear
public record CreateOrderRequest(long UserId, List<long> ProductIds);
public record CreateOrderResult(long OrderId, decimal Total);

// Just add [UseCase] - automatic DI registration, no configuration needed
[UseCase]
public class CreateOrderUseCase(
    IOrderRepository orders,
    IEventDispatcher eventDispatcher) : ICreateOrderUseCase
{
    public async Task<CreateOrderResult> Execute(CreateOrderRequest request)
    {
        // Pure business logic - no HTTP, no infrastructure
        var order = await orders.Create(request.UserId, request.ProductIds);

        // Events work locally or distributed
        eventDispatcher.Dispatch(new OrderCreated { OrderId = order.Id });

        return new CreateOrderResult(order.Id, order.Total);
    }
}
```

**Why Use Cases?**

✅ **Zero boilerplate** - One attribute, automatic registration, that's it

✅ **Trivial to test** - Constructor injection, no mocks for the framework, just your dependencies

✅ **Single responsibility** - One use case = one business workflow = easy to understand

✅ **Reusable** - Use cases can call other use cases, building complex workflows from simple pieces

✅ **Framework agnostic** - Works with any web framework, gRPC, message queues, CLI tools

**Testing is trivial:**
```csharp
// Just instantiation - no mocking the framework
var orders = new FakeOrderRepository();
var events = new FakeEventDispatcher();
var useCase = new CreateOrderUseCase(orders, events);

var result = await useCase.Execute(new CreateOrderRequest(userId: 1, productIds: [2, 3]));

Assert.Equal(expectedOrderId, result.OrderId);
```

**Chaining use cases is instant:**
```csharp
// Complex workflows are just composition
[UseCase]
public class ProcessOrderUseCase(
    ICreateOrderUseCase createOrder,
    IChargePaymentUseCase chargePayment,
    ISendConfirmationUseCase sendConfirmation) : IProcessOrderUseCase
{
    public async Task Execute(ProcessOrderRequest request)
    {
        // Each step is a tested, reusable use case
        var order = await createOrder.Execute(new(request.UserId, request.ProductIds));
        await chargePayment.Execute(new(order.OrderId, request.PaymentMethod));
        await sendConfirmation.Execute(new(order.OrderId));
    }
}
```

Build complex business processes in minutes, not days.

### Event-Driven Architecture

**Decouple your system with events** - The same event code works in-memory, with Kafka, or AWS SQS.

**Dispatch events from anywhere:**
```csharp
[UseCase]
public class CreateOrderUseCase(
    IOrderRepository orders,
    IEventDispatcher eventDispatcher) : ICreateOrderUseCase
{
    public async Task Execute(CreateOrderRequest request)
    {
        var order = await orders.Create(request.UserId, request.ProductIds);

        // Dispatch event - works locally or distributed
        eventDispatcher.Dispatch(new OrderCreated
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Total = order.Total
        });

        return new CreateOrderResult(order.Id, order.Total);
    }
}
```

**Handle events in separate use cases:**
```csharp
// Handlers are just use cases - automatically registered
[UseCase]
public class SendOrderConfirmationHandler(
    IEmailService emails) : IEventHandler<OrderCreated>
{
    public async Task Handle(OrderCreated evt)
    {
        // Runs async - doesn't block the order creation
        await emails.SendOrderConfirmation(evt.OrderId);
    }
}

[UseCase]
public class UpdateInventoryHandler(
    IInventoryRepository inventory) : IEventHandler<OrderCreated>
{
    public async Task Handle(OrderCreated evt)
    {
        // Multiple handlers process the same event independently
        await inventory.DecrementStock(evt.OrderId);
    }
}
```

**Why events?**

✅ **Decouple your code** - Emit events without knowing who's listening

✅ **Scale independently** - Handlers run async, don't block the caller

✅ **Add features without breaking existing code** - New handler = new capability

✅ **Same interface everywhere** - Local development, Kafka production, SQS on AWS

✅ **Testable** - Verify events were dispatched, test handlers in isolation

```csharp
// Switch from local to distributed with zero code changes
services.RegisterLocalMagicEvents();          // Development
services.RegisterMagicKafkaEvents(config);    // Production
services.RegisterMagicSQSEvents(config);      // AWS

// Your code never changes
eventDispatcher.Dispatch(new OrderCreated { ... });
```

### Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register use cases
builder.Services.AddMagicUseCases();

// Choose your event strategy
builder.Services.RegisterLocalMagicEvents();        // Local development
// builder.Services.RegisterMagicKafkaEvents(config);  // Production with Kafka

// Setup repositories with Snowflake IDs
builder.Services.RegisterSnowflakeKeyGen(generatorId: 1);
```

That's it. Clean, testable, production-ready.

## Complete Example

Want to see all the patterns in action? Check out the **OrderManagement** example - a complete, production-ready implementation demonstrating every MagicCSharp pattern.

### What's Included

The [OrderManagement example](examples/OrderManagement/) is a full-featured REST API showcasing:

**Three-Layer Architecture in Practice**
- Controllers handling HTTP concerns (DTOs, status codes)
- Use Cases with pure business logic
- Repository pattern with Entity Framework
- Clean separation at every layer

**Event-Driven Workflows**
- Event dispatching from use cases
- Multiple independent event handlers
- Event chaining (events triggering events)
- Async processing without blocking

**Use Case Patterns**
- Request/Result pattern for clear contracts
- Use case chaining for complex workflows
- Automatic DI registration with `[UseCase]`
- Testable design with constructor injection

**Production Infrastructure**
- Snowflake ID generation for distributed systems
- Entity Framework with migrations
- Swagger documentation
- Logging and observability

### Try It Yourself

```bash
cd examples/OrderManagement
dotnet restore
dotnet run --project OrderManagement.Api

# Visit https://localhost:5001/swagger
```

**Example APIs:**
- `POST /api/orders` - Create an order (triggers events)
- `POST /api/orders/{id}/payment` - Process payment
- `POST /api/orders/process` - Create + pay in one request (use case chaining)
- `GET /api/orders/user/{userId}` - Get user's orders

Watch the logs to see event-driven architecture in action - events flowing through handlers asynchronously!

[📖 Full Example Documentation](examples/OrderManagement/OrderManagement.Api/README.md)

## The MagicCSharp Ecosystem

### [MagicCSharp](src/MagicCSharp/) - Core Framework

The foundation for clean architecture applications.

**Key Features:**
- Use Case pattern with automatic DI registration
- Three-layer architecture enforcement
- Distributed locking across instances
- Scheduled background services without drift
- Request ID tracking across async calls
- Testable time with `IClock`

[📖 Full Documentation](src/MagicCSharp/README.md)

### [MagicCSharp.Data](src/MagicCSharp.Data/) - Data Access

Repository pattern and data infrastructure for distributed systems.

**Key Features:**
- Repository pattern with `IRepository<TEntity>`
- Snowflake ID generation for distributed databases
- Globally unique, time-sortable IDs
- Multi-instance coordination
- Clean separation between domain and data access

[📖 Full Documentation](src/MagicCSharp.Data/README.md)

### [MagicCSharp.Events](src/MagicCSharp.Events/) - Event-Driven Architecture

Build event-driven systems that work locally or distributed.

**Key Features:**
- Unified `IEventDispatcher` interface
- Local events for development
- Kafka events for production
- SQS events for AWS environments
- OpenTelemetry metrics
- Switch implementations without code changes

**Packages:**
- `MagicCSharp.Events` - Core event infrastructure
- `MagicCSharp.Events.Kafka` - Kafka implementation
- `MagicCSharp.Events.SQS` - AWS SQS implementation

[📖 Full Documentation](src/MagicCSharp.Events/README.md)

## Real-World Benefits

### For Startups
- **Move fast** - Focus on features, not infrastructure
- **Scale when ready** - Start simple, scale later without rewrites
- **Onboard quickly** - Clean patterns developers already know

### For Enterprises
- **Consistent architecture** - Same patterns across all services
- **Testable by design** - `IClock`, dependency injection, clean separation
- **Production-proven** - Distributed locking, event-driven, background jobs

### For Teams
- **Clear boundaries** - Controllers, Use Cases, Services separation
- **Easy code review** - Consistent patterns across the codebase
- **Maintainable** - Business logic isolated from infrastructure

## The Story

**Every great framework starts with a pain point.**

After years at Amazon as a Senior Engineer and later Senior Engineering Manager, I knew what it took to build systems that scale. I'd seen it firsthand, massive distributed systems serving millions of customers. But there was one constant frustration: being forced to write Java when my heart was with C#.

When I founded MagicDoor in 2023, the decision was immediate: **we'd be a C# shop.** Finally, I could build with the language I loved.

Then reality hit.

C# had great frameworks for building APIs. Powerful libraries for data access. But when it came to structuring enterprise-grade applications? There was no clear, well-defined path. No comprehensive answer to the questions that matter:

*"How do we write code that's testable from day one?"*

*"How do we build for distributed systems without painting ourselves into a corner?"*

*"How do we move fast early but not create technical debt later?"*

At Amazon scale, you learn what distributed systems demand:
- **Clean separation** so teams can work independently
- **Event-driven architecture** that scales horizontally
- **Infrastructure that starts simple** but seamlessly transitions to production
- **Patterns that prevent technical debt** before it starts

**We couldn't find a C# framework or pattern that gave us all of this. So we built it.**

MagicCSharp is born from real-world pain points, battle-tested in production at MagicDoor, and designed with one purpose: **let C# developers build enterprise-grade, distributed systems without fighting their tools.**

This is the framework I wish I had at Amazon.
This is the framework every C# team deserves.

— Kasper Sogaard, Founder

## Philosophy

**Conventions over configuration** - Attributes and marker interfaces over XML configs

**Interface-based** - Program to interfaces, swap implementations freely

**Clean architecture** - Business logic stays pure and testable

**Distributed-first** - Built for multi-instance from the ground up

**Zero-compromise** - Local simplicity, distributed power

## Learn More

**Example Application:**
- [OrderManagement Example](examples/OrderManagement/OrderManagement.Api/README.md) - Complete working example

**Framework Documentation:**
- [MagicCSharp Core](src/MagicCSharp/README.md) - Use cases, scheduling, locking
- [MagicCSharp.Data](src/MagicCSharp.Data/README.md) - Repositories and Snowflake IDs
- [MagicCSharp.Events](src/MagicCSharp.Events/README.md) - Event-driven architecture
- [MagicCSharp.Events.Kafka](src/MagicCSharp.Events.Kafka/README.md) - Kafka integration
- [MagicCSharp.Events.SQS](src/MagicCSharp.Events.SQS/README.md) - AWS SQS integration

## License

MIT License - See LICENSE file for details.

---

**Ready to build enterprise-grade applications without the enterprise-grade complexity?**

```bash
dotnet add package MagicCSharp
```
