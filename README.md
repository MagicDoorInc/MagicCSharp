# MagicCSharp

**Stop writing infrastructure code. Start building features.**

MagicCSharp is a complete toolkit for building enterprise-grade, distributed C# applications with clean architecture patterns. Go from prototype to production-ready distributed systems without the boilerplate.

Eleven packages, split so you take only what you use. See [CHANGELOG.md](CHANGELOG.md) for what changed and how
to migrate.

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

✅ **Drift-Free Scheduling** - Background jobs that stay on schedule, coordinated across instances

✅ **Snowflake IDs** - Globally unique, time-sortable IDs for distributed databases

✅ **Public Keys** - Unguessable string keys for anything a user can see, in an alphabet without `0`/`O` or `I`/`l`

✅ **Repositories** - CRUD, batch writes, pagination, soft delete and free-text search over Entity Framework

✅ **Request Tracking** - Trace requests across async boundaries

✅ **Testable Time** - Mock time in tests with `IClock`

✅ **Errors That Make Sense** - Domain exceptions become RFC 7807 responses; a missing row is a 404, not a 500

✅ **Fails At Startup, Not In Production** - Every registration resolved before the first request

✅ **Test Doubles** - A controllable clock, deterministic ids, synchronous events, and repository tests against real PostgreSQL

✅ **Scaffolding** - Generate an entity across its four files, and lint the conventions the compiler can't

## Quick Start

### The fast path

Scaffold a repository and a running service:

```bash
dotnet tool install -g MagicCSharp.Cli

mcs init --prefix Acme
mcs create-app --name Shop --database shop
dotnet run --project Apps/Shop/Shop.App
```

That gives you a service that builds, boots and answers — with use cases, `IClock`, Snowflake IDs,
request-ID tracking, events, scheduling and problem-details error handling already wired.
[More on the repository layout ↓](#the-repository-layout--optional)

### Adding it to an existing project

One package, two calls:

```bash
dotnet add package MagicCSharp.App
```

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddMagicApp();

var app = builder.Build();
app.UseMagicApp(builder);
app.Run();
```

`MagicCSharp.App` brings in the core, ASP.NET, events and scheduling packages and wires them in the order
they need. Pass `MagicAppOptions` to change any of it.

### Or take only the pieces you want

Every package stands alone. The core has three dependencies and knows nothing about ASP.NET, Entity
Framework or Kafka, so a worker or a console app takes only what it uses.

```bash
# Core - use cases, IClock, Snowflake ids and public keys, request IDs
dotnet add package MagicCSharp

# Data - repository contracts, pagination, filter helpers (no persistence library)
dotnet add package MagicCSharp.Data
dotnet add package MagicCSharp.Data.EntityFramework   # the implementations
dotnet add package MagicCSharp.Data.Postgres          # pooled factory, migrations, UTC interceptor

# Events
dotnet add package MagicCSharp.Events
dotnet add package MagicCSharp.Events.Kafka           # optional transport
dotnet add package MagicCSharp.Events.SQS             # optional transport

# The rest, as needed
dotnet add package MagicCSharp.AspNetCore             # request-ID middleware, error handling
dotnet add package MagicCSharp.Scheduling             # drift-free background jobs

# Test projects
dotnet add package MagicCSharp.Testing                # fakes, no heavy dependencies
dotnet add package MagicCSharp.Testing.Database       # repository tests on real PostgreSQL
```

### Define a Use Case

**Use cases are just classes** - No base classes, no framework coupling, just pure C# with a marker interface.

```csharp
// Request/Result pattern keeps contracts clear
public record CreateOrderRequest(long UserId, List<long> ProductIds);
public record CreateOrderResult(long OrderId, decimal Total);

// Define interface with IMagicUseCase marker
public interface ICreateOrderUseCase : IMagicUseCase
{
    Task<CreateOrderResult> Execute(CreateOrderRequest request);
}

// Implementation - [MagicUseCase] attribute is optional (defaults to Scoped)
// Add attribute only if you need a different lifetime (Singleton, Transient)
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

✅ **Zero boilerplate** - Automatic registration, no configuration needed (attribute optional)

✅ **Trivial to test** - Constructor injection, no mocks for the framework, just your dependencies

✅ **Single responsibility** - One use case = one business workflow = easy to understand

✅ **Reusable** - Use cases can call other use cases, building complex workflows from simple pieces

✅ **Framework agnostic** - Works with any web framework, gRPC, message queues, CLI tools

**Testing is trivial:**
```csharp
// Just instantiation - no mocking the framework
var orders = new FakeOrderRepository();
var events = new SyncEventDispatcher(asyncDispatcher);
var useCase = new CreateOrderUseCase(orders, events);

var result = await useCase.Execute(new CreateOrderRequest(userId: 1, productIds: [2, 3]));

Assert.Equal(expectedOrderId, result.OrderId);
Assert.True(events.HasDispatchedEvent<OrderCreated>());
```

`MagicCSharp.Testing` supplies the doubles for the framework's own seams — a clock you move by hand, ids
derived from it, an event dispatcher that runs handlers inline so you can assert without sleeping, and an
in-memory distributed lock. Anything time-dependent becomes testable in milliseconds:

```csharp
clock.SetTime(2026, 3, 1);
await createLease.Execute(request);

clock.AdvanceDays(31);
await applyLateFees.Execute();

Assert.Single(await fees.Get(new FeeFilter { LeaseId = leaseId }));
```

**Chaining use cases is instant:**
```csharp
// Complex workflows are just composition
public interface IProcessOrderUseCase : IMagicUseCase
{
    Task Execute(ProcessOrderRequest request);
}

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
// Event handlers are automatically registered - no attribute needed
public class SendOrderConfirmationHandler(
    IEmailService emails) : IEventHandler<OrderCreated>
{
    public async Task Handle(OrderCreated evt)
    {
        // Runs async - doesn't block the order creation
        await emails.SendOrderConfirmation(evt.OrderId);
    }
}

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
- Automatic DI registration (no attributes required)
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

Twelve packages, split so you take only what you use — plus `MagicCSharp.App`, which bundles the four a web
service needs when you would rather not choose. The core has three dependencies and knows nothing about
ASP.NET, Entity Framework or Kafka.

| Package | Add it when you want | Brings with it |
|---|---|---|
| **[MagicCSharp.App](src/MagicCSharp.App/)** | A web service wired in two calls | the four below it |
| **[MagicCSharp](src/MagicCSharp/)** | Use cases, `IClock`, Snowflake ids and public keys, request IDs, `Optional<T>` | DI + logging abstractions, IdGen |
| **[MagicCSharp.AspNetCore](src/MagicCSharp.AspNetCore/)** | Request-ID middleware, RFC 7807 error handling, startup preflight | the ASP.NET shared framework |
| **[MagicCSharp.Scheduling](src/MagicCSharp.Scheduling/)** | Drift-free background jobs, one instance per occurrence | DistributedLock, hosting |
| **[MagicCSharp.Data](src/MagicCSharp.Data/)** | Repository contracts, pagination, LINQ filter helpers | nothing — no persistence library |
| **[MagicCSharp.Data.EntityFramework](src/MagicCSharp.Data.EntityFramework/)** | The repository base classes and DALs | EF Core |
| **[MagicCSharp.Data.Postgres](src/MagicCSharp.Data.Postgres/)** | Pooled context factory, design-time factory, UTC interceptor | Npgsql |
| **[MagicCSharp.Events](src/MagicCSharp.Events/)** | Event dispatch and handler discovery | System.Text.Json |
| **[MagicCSharp.Events.Kafka](src/MagicCSharp.Events.Kafka/)** | Kafka transport | Confluent.Kafka |
| **[MagicCSharp.Events.SQS](src/MagicCSharp.Events.SQS/)** | SQS transport | AWSSDK.SQS |
| **[MagicCSharp.Testing](src/MagicCSharp.Testing/)** | `FakeClock`, `FakeKeyGen`, synchronous events, in-memory locks | DistributedLock |
| **[MagicCSharp.Testing.Database](src/MagicCSharp.Testing.Database/)** | Repository tests against real PostgreSQL | Testcontainers, xUnit |

A domain project referencing `MagicCSharp.Data` gets the repository interfaces and no persistence library at
all — which is the point of the split. Wanting `FakeClock` does not mean wanting Docker.

### The repository layout — optional

The packages above work in any project structure. Separately, MagicCSharp offers the structure MagicDoor runs
its own backend on. Take it, take part of it, or ignore it — nothing in the packages reads it.

```
Apps/Shop/
  Shop.App/                  host: Program.cs, controllers
  Shop.Domains/Orders/
    Default/                 use cases, event handlers
    Models/                  entities, edits, filters
    Tests/
  Data/
    Data.Models/             repository interfaces — no EF dependency
    Data.EntityFramework/    DALs, repositories, context, migrations
Libs/                        code more than one service uses
```

#### What it buys you

**Several services, one repository, no version dance.** A change that spans two services is one commit and
one pull request, not a package publish and a wait. Shared code lives in `Libs/` and is referenced directly,
so there is no version of it to be behind.

**But you still build one service at a time.** Each gets its own `.slnx`. You open `Acme.Shop.slnx` and
build the projects you are working on; `Acme.All.slnx` exists for the times you need to see everything, and
is regenerated from disk so it is never a merge conflict worth resolving.

**The domain does not know how it is stored.** `Order` lives in the domain that owns it; `OrderDal` and
`OrdersEfRepository` live under `Data/`. `Data.Models` holds only interfaces and does not reference Entity
Framework at all, so the arrow points from storage toward the domain and never back. You can read a domain
without reading a single EF attribute, and swap what is underneath without touching the logic.

**Every entity looks the same.** `mcs add-entity` writes the four files an entity needs — across three
projects, each of which has to agree about names, namespaces and generic arguments — and registers it. That
is not typing saved so much as a class of mistake removed, and it means anyone can open any service and
recognise what they are looking at.

**The conventions are checked, not just agreed.** `mcs validate` catches the things that compile and fail
later: a `DateTime.Now` that makes behaviour untestable, an event carrying an entity that will deserialize
to nulls after a deploy. It exits non-zero, so CI can hold the line instead of a reviewer.

#### Getting it

```bash
dotnet tool install -g MagicCSharp.Cli

mcs init --prefix Acme                     # or in a repo you already have
mcs create-app --name Shop --database shop
```

For a team, `dotnet new magiccsharp-repo -n Acme` scaffolds the same thing plus a tool manifest, so everyone
runs one pinned version of the scaffolding after `dotnet tool restore`.

`mcs` also creates domains and shared libraries, and rebuilds the wide solution. Nothing is ever overwritten
— an existing file is reported and skipped — and re-running any command produces no diff.

#### Generated code you own

Every file `mcs` writes comes from a template, and you can replace any one of them with your own:

```bash
mcs templates list                        # all 19, and where each comes from
mcs templates eject Entities/dal.cs.hbs   # copy one into your repo to edit
```

Your copy lands in `.magiccsharp/templates/`. **Commit it** — that directory is how the override reaches
everyone else:

```bash
git add .magiccsharp && git commit -m "Use our own DAL template"
```

From then on it wins over the built-in for everyone who pulls. Deleting it reverts — there's no registry or
cache anywhere else.

The important part is that it works **per file**. Take over the DAL template to add your audit columns and
the other eighteen still come from the tool, still improving as it does. That's the difference between
customising a generator and forking one.

Across several repositories, put the templates in a repository of their own and add it as a submodule at
`.magiccsharp/templates`, so house style is defined once instead of copied around.

**[Full guide →](docs/repository-layout.md)** · [Template overrides →](docs/template-overrides.md) · [CLI reference →](src/MagicCSharp.Cli/README.md)

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
