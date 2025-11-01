# OrderManagement - Development Guide

**IMPORTANT**: Never add co-author to git commits or git PRs

## 🤖 AI Development Guidelines

**CRITICAL: Before implementing or modifying code, you MUST consult the relevant knowledge file:**

| Working On | Consult Knowledge File |
|------------|----------------------|
| **Repositories, DAL, Entities, Filters** | `.ai-knowlage/repositories-and-data-storage.md` |
| **Controllers, Use Cases, Services** | `.ai-knowlage/separation-of-concerns.md` |
| **Use Case Implementation** | `.ai-knowlage/use-cases-deep-dive.md` |
| **Code Style, Naming, Patterns** | `.ai-knowlage/coding-standards.md` |

**These knowledge files contain the authoritative patterns and standards. Always review the relevant file before starting work.**

---

---

## 🔴 CRITICAL: Use Rider MCP Tools for Refactoring

**IMPORTANT: When renaming, moving, or refactoring files, ALWAYS use JetBrains Rider MCP tools!**

The following MCP tools are available and should be preferred over manual file operations:

### File Operations
- **`mcp__jetbrains__rename_refactoring`** - Rename symbols (classes, methods, variables, etc.)
  - Automatically updates all references across the entire project
  - Updates namespaces when files are moved
  - Safer than text search/replace

### Why Use Rider MCP Tools?
- ✅ **Automatic namespace updates** - Rider updates namespaces when files are moved
- ✅ **Reference tracking** - All usages are updated automatically
- ✅ **Type-safe** - Understands code structure, not just text
- ✅ **Prevents errors** - No missing references or broken builds

### When to Use Rider Tools vs Manual Tools
```
✅ USE RIDER MCP:
- Renaming classes, methods, variables, properties
- Moving files between folders (use rename with path change)
- Refactoring code structure

❌ DON'T USE git mv / Edit / Write FOR:
- Renaming C# symbols
- Moving files that require namespace changes
- Refactoring operations that affect multiple files

✅ USE git mv / Edit / Write FOR:
- Moving non-code files (templates, configs, etc.)
- Bulk operations where namespace changes are handled separately
- Simple text replacements in comments or strings
```

### Available Rider MCP Tools
- `mcp__jetbrains__rename_refactoring` - Rename symbols intelligently
- `mcp__jetbrains__get_file_problems` - Check for errors before refactoring
- `mcp__jetbrains__search_in_files_by_text` - Find usages before changes
- `mcp__jetbrains__reformat_file` - Apply code formatting

**Always prefer Rider MCP tools for C# code refactoring operations!**

---

## Project Overview

**OrderManagement** is an example project demonstrating the MagicCSharp framework patterns for building clean, maintainable .NET applications.

### Architecture

```
OrderManagement/
├── OrderManagement.Api/         # API endpoints (HTTP layer)
├── OrderManagement.Business/    # Business logic (Use Cases)
├── OrderManagement.Data/        # Data access (Repositories & Entities)
└── .ai-knowlage/               # ⭐ AI-accessible pattern documentation
```

**Three-Layer Architecture:**
- **Api (Controllers)** → HTTP concerns only → See `separation-of-concerns.md`
- **Business (Use Cases)** → Business logic → See `use-cases-deep-dive.md`
- **Data (Repositories)** → Data access → See `repositories-and-data-storage.md`

## MagicCSharp Framework

This project uses the **MagicCSharp** framework which provides:

- **BaseIdRepository** - CRUD operations with filtering, batch operations, automatic timestamping
- **Use Case Pattern** - Business logic orchestration with `[UseCase]` attribute
- **DAL Pattern** - Structured data access layer with Entity/Edit/Filter separation
- **Filter Extensions** - Reusable query patterns (ApplyListFilter, ApplyStringNullableValueFilter, etc.)

## Quick Command Reference

### Development
```bash
# Build and run
dotnet restore
dotnet build
dotnet run --project OrderManagement.Api

# Testing
dotnet test

# Database migrations (if using EF)
dotnet ef migrations add <MigrationName> --project OrderManagement.Data
dotnet ef database update --project OrderManagement.Data
```

## Knowledge Files Reference

The `.ai-knowlage/` folder contains quick reference guides for all development patterns:

1. **`repositories-and-data-storage.md`**
   - Repository pattern with BaseIdRepository
   - DAL (Data Access Layer) structure
   - Filter extensions and query patterns
   - Sub-entity management
   - Query strategy: Always use Get with Filters

2. **`separation-of-concerns.md`**
   - Three-layer architecture (Controllers/Use Cases/Services)
   - Decision matrix: which layer for what
   - Data flow patterns
   - Error handling by layer

3. **`use-cases-deep-dive.md`**
   - IUseCase interface and patterns
   - Request/Response records
   - [UseCase] attribute for DI
   - Orchestration patterns

4. **`coding-standards.md`**
   - Primary constructors (always)
   - No "Async" suffix (never)
   - No underscore prefix (never)
   - Trailing commas, explicit types
   - Naming conventions

## Core Standards (Quick Reference)

**See `.ai-knowlage/coding-standards.md` for complete standards**

### Critical Rules
- ✅ **Always** use primary constructors
- ✅ **Always** use explicit types in `new` expressions
- ✅ **Always** use trailing commas
- ❌ **Never** postfix methods with "Async"
- ❌ **Never** prefix fields with underscore
- ❌ **Never** use `new()` without explicit type

### Naming Conventions
- Classes: `PascalCase`
- Interfaces: `IPascalCase`
- Methods: `PascalCase` (no Async suffix)
- Variables: `camelCase` (no underscore prefix)
- Constants: `PascalCase`

## DAL Pattern Quick Reference

### Required Structure
- **DAL Class**: Database entity with EF annotations
- **Entity Record**: Business object (inherits from Edit)
- **Edit Record**: Mutable properties
- **Filter Class**: Query criteria

### DAL Required Methods
```csharp
public class OrderDal : IDalTransform<Order, OrderEdit>, IDalId, IDal
{
    // 1. Convert DAL → Entity
    public Order ToEntity() { }

    // 2. Update DAL from Edit
    public void Apply(OrderEdit edit) { }

    // 3. Create DAL from Edit
    public static OrderDal From(OrderEdit edit, long id)
    {
        var dal = new OrderDal { Id = id };
        dal.Apply(edit);
        return dal;
    }
}
```

## Use Case Pattern Quick Reference

### Interface Pattern
```csharp
public interface IUseCase { }  // Marker interface

public interface ICreateOrderUseCase : IUseCase
{
    Task<Order> Execute(CreateOrderRequest request);
}
```

### Implementation Pattern
```csharp
[UseCase]  // Auto-registers in DI
public class CreateOrderUseCase(
    IOrderRepository orderRepository,
    ICustomerRepository customerRepository) : ICreateOrderUseCase
{
    public async Task<Order> Execute(CreateOrderRequest request)
    {
        // Implement business logic here
    }
}
```

## Repository Pattern Quick Reference

### Query Strategy: Always Use Get with Filters

**CRITICAL: 99% of queries should use the `Get(filter)` method.**

```csharp
// ✅ Use Get with filter - Handles 99% of queries
var orders = await repository.Get(new OrderFilter
{
    CustomerIds = [customerId],
    Status = OrderStatus.Pending,
    CreatedAfter = DateTimeOffset.UtcNow.AddDays(-30),
});

// ✅ Single result - Get returns list, take first
var latestOrder = (await repository.Get(new OrderFilter
{
    CustomerIds = [customerId],
    Limit = 1,
    OrderBy = OrderByDirection.Descending,
})).FirstOrDefault();
```

## Development Workflow

1. **Review relevant knowledge file first** (see table at top)
2. Implement following established patterns
3. Write unit tests (use cases only)
4. Create migration if database changes
5. Update API documentation if new endpoints

**Remember:** The knowledge files contain the authoritative patterns. Always consult them before implementing.

## Related Documentation

- **`.ai-knowlage/`** - Development patterns and standards
- **MagicCSharp Framework** - Located in `../../src/MagicCSharp/`
