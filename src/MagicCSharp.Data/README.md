# MagicCSharp.Data

**Type-safe repository pattern with Entity Framework Core**

Stop writing repetitive data access code. MagicCSharp.Data provides generic repository base classes with built-in pagination, automatic timestamp tracking, and customizable hooks for your business logic.

## Why MagicCSharp.Data?

✅ **Less Boilerplate** - Generic repository handles 90% of your data access code

✅ **Type-Safe** - Full IntelliSense support with generic constraints

✅ **Pagination Built-In** - No more manual skip/take calculations

✅ **Automatic Timestamps** - Created/Updated fields managed for you

✅ **Customizable Hooks** - Inject business logic at the right points

✅ **Testable** - Easy to mock for unit tests

## Installation

```bash
dotnet add package MagicCSharp.Data
dotnet add package MagicCSharp
```

## Quick Start

### Understanding the Pattern

MagicCSharp.Data uses a three-layer pattern to separate concerns:

1. **Edit Record** - Mutable properties for creating/updating
2. **Entity Record** - Immutable business object (extends Edit)
3. **DAL Class** - Database entity with EF Core annotations

```
Edit (create/update) → DAL (database) → Entity (business logic)
```

### 1. Define Your Entities

**IMPORTANT:** Entities should be records that inherit from Edit:

```csharp
// 1. Edit - Mutable properties only
public record ProductEdit
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public long CategoryId { get; init; }
}

// 2. Entity - Business object (inherits Edit + adds Id and timestamps)
public record Product : ProductEdit, IMagicEntity, IIdEntity
{
    public long Id { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset Updated { get; init; }
}
```

**Why this pattern?**
- Edit has only the properties that can be changed
- Entity adds Id and timestamps that are set by the system
- Prevents accidental modification of Id or timestamps in business logic

### 2. Define Your DAL (Data Access Layer)

**CRITICAL:** Configure ALL database structure using annotations, NOT in DbContext.OnModelCreating():

```csharp
[Table("products")]                                  // ✅ snake_case table name
[Index(nameof(CategoryId))]                          // ✅ Index foreign keys
[Index(nameof(Name))]                                // ✅ Index queried fields
public class ProductDal : IDalTransform<Product, ProductEdit>, IDalId, IDal
{
    // ✅ Use Snowflake IDs, NOT auto-increment
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // IMPORTANT: No auto-increment!
    [Column("id")]
    public long Id { get; set; }

    [Required]                                        // NOT NULL constraint
    [Column("name")]
    [StringLength(200)]                               // VARCHAR(200)
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("price")]
    [Precision(18, 2)]                                // DECIMAL(18,2)
    public decimal Price { get; set; }

    [Required]
    [Column("category_id")]
    public long CategoryId { get; set; }

    // Automatic timestamps (handled by BaseIdRepository)
    [Required]
    [Column("created")]
    public DateTimeOffset Created { get; set; }

    [Required]
    [Column("updated")]
    public DateTimeOffset Updated { get; set; }

    // 1. Convert DAL → Entity
    public Product ToEntity() => new()
    {
        Id = Id,
        Name = Name,
        Price = Price,
        CategoryId = CategoryId,
        Created = Created,
        Updated = Updated
    };

    // 2. Update DAL from Edit (for updates)
    public void Apply(ProductEdit edit)
    {
        Name = edit.Name;
        Price = edit.Price;
        CategoryId = edit.CategoryId;
    }

    // 3. Create DAL from Edit (for inserts)
    public static ProductDal From(ProductEdit edit, long id)
    {
        var dal = new ProductDal { Id = id };
        dal.Apply(edit);
        return dal;
    }
}
```

**DAL Rules:**
- ✅ Use `[Table("snake_case")]` for table names
- ✅ Use `[Column("snake_case")]` for column names
- ✅ Use `[Index(nameof(Property))]` on foreign keys and queried fields
- ✅ Use `[DatabaseGenerated(DatabaseGeneratedOption.None)]` - IDs come from Snowflake generator
- ✅ Use `[Required]` for NOT NULL columns
- ✅ Use `[StringLength(n)]` for VARCHAR columns
- ✅ Use `[Precision(n, d)]` for DECIMAL columns
- ❌ Never configure tables in `DbContext.OnModelCreating()`
- ❌ Never use auto-increment IDs

### 3. Define Your Filter

```csharp
public record ProductFilter
{
    public List<long>? Ids { get; init; }
    public List<long>? CategoryIds { get; init; }
    public string? Name { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public DateTimeOffset? CreatedAfter { get; init; }
    public int? Limit { get; init; }
    public OrderByDirection? OrderBy { get; init; }
}
```

### 4. Create Your Repository

**IMPORTANT:** Repositories require `IKeyGenService` for Snowflake ID generation:

```csharp
public class ProductRepository(
    IKeyGenService keyGenService,                    // ✅ For Snowflake IDs
    IDbContextFactory<AppDbContext> contextFactory,
    IClock clock)
    : BaseIdRepository<AppDbContext, ProductDal, Product, ProductFilter, ProductEdit>(
        contextFactory, clock)
{
    // Apply filter using built-in extension methods
    protected override IQueryable<ProductDal> ApplyFilter(
        IQueryable<ProductDal> query,
        ProductFilter filter)
    {
        // ✅ Use ApplyListFilter for list filtering (handles null/empty automatically)
        query = query.ApplyListFilter(filter.Ids, x => x.Id);
        query = query.ApplyListFilter(filter.CategoryIds, x => x.CategoryId);

        // ✅ Use ApplyStringNullableValueFilter for string filtering
        query = query.ApplyStringNullableValueFilter(
            filter.Name,
            x => x.Name,
            StringFilterOperation.Contains);

        // Manual filtering for ranges
        if (filter.MinPrice.HasValue)
            query = query.Where(x => x.Price >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query = query.Where(x => x.Price <= filter.MaxPrice.Value);

        if (filter.CreatedAfter.HasValue)
            query = query.Where(x => x.Created >= filter.CreatedAfter.Value);

        return query;
    }

    protected override DbSet<ProductDal> GetDbSet(AppDbContext context)
        => context.Products;

    // ✅ Use keyGenService.GetId() for Snowflake IDs
    protected override ProductDal CreateDal(ProductEdit edit)
        => ProductDal.From(edit, keyGenService.GetId());

    protected override NotFoundException GetNotFoundException(long id)
        => new NotFoundIdException(id, nameof(Product));
}
```

**Repository Rules:**
- ✅ Always inject `IKeyGenService` for ID generation
- ✅ Use `keyGenService.GetId()` in `CreateDal()` - generates Snowflake IDs
- ✅ Use filter extension methods for consistency
- ✅ Return `NotFoundIdException(id, nameof(Entity))` for 404s
- ❌ Never generate IDs manually
- ❌ Never use auto-increment or `Guid.NewGuid()`

### 5. Use Your Repository

```csharp
public class ProductService(ProductRepository repository)
{
    public async Task<Product> CreateProduct(ProductEdit edit)
    {
        return await repository.Create(edit);
    }

    public async Task<IReadOnlyList<Product>> SearchProducts(ProductFilter filter)
    {
        return await repository.Get(filter);
    }

    public async Task<Pagination<Product>> GetPaginatedProducts(
        ProductFilter filter,
        int page,
        int pageSize)
    {
        return await repository.Get(
            new PaginationRequest(page, pageSize),
            filter);
    }
}
```

## Query Strategy: Always Use Get() with Filters

**CRITICAL: 99% of queries should use the `Get(filter)` method. Only create custom query methods when absolutely necessary.**

### ✅ Correct Pattern (Use This)

```csharp
// ✅ Use Get with filter - Handles 99% of queries
var products = await repository.Get(new ProductFilter
{
    CategoryIds = [categoryId],
    MinPrice = 10.00m,
    CreatedAfter = DateTimeOffset.UtcNow.AddDays(-7),
});

// ✅ Single result - Get returns list, take first
var latestProduct = (await repository.Get(new ProductFilter
{
    CategoryIds = [categoryId],
    Limit = 1,
    OrderBy = OrderByDirection.Descending,
})).FirstOrDefault();

// ✅ Complex filtering - still use Get
var results = await repository.Get(new ProductFilter
{
    Name = "laptop",              // Contains search
    MinPrice = 500,
    CategoryIds = [1, 2, 3],      // Multiple categories
    CreatedAfter = yesterday,
    Limit = 50,
});
```

### ❌ Wrong Pattern (Avoid This)

```csharp
// ❌ Don't create custom methods for simple queries
public async Task<List<Product>> GetByCategory(long categoryId)
{
    // This should use Get with filter instead!
}

// ❌ Don't create custom methods for single results
public async Task<Product?> GetLatestByCategory(long categoryId)
{
    // This should use Get with filter + FirstOrDefault instead!
}

// ❌ Don't write manual LINQ queries in services
public async Task<List<Product>> GetExpensiveProducts()
{
    using var context = await contextFactory.CreateDbContextAsync();
    return await context.Products.Where(p => p.Price > 1000).ToListAsync();
    // Use repository.Get() with filter instead!
}
```

### When to Create Custom Methods

Only create custom query methods when:
1. Query requires complex joins across multiple tables
2. Query uses advanced SQL features (window functions, CTEs, etc.)
3. Query has complex aggregations or grouping
4. Performance requires raw SQL or stored procedure

**Example of legitimate custom method:**
```csharp
// ✅ Acceptable - Complex query with aggregation
public async Task<Dictionary<long, decimal>> GetAveragePriceByCategory()
{
    using var context = await contextFactory.CreateDbContextAsync();
    return await context.Products
        .GroupBy(p => p.CategoryId)
        .Select(g => new { CategoryId = g.Key, AvgPrice = g.Average(p => p.Price) })
        .ToDictionaryAsync(x => x.CategoryId, x => x.AvgPrice);
}
```

**Key Rule:** If you can express the query with filter properties → Use Get() with filter. Always.

## Filter Extensions

MagicCSharp.Data provides extension methods for consistent, safe filtering:

| Extension | Use Case | Example |
|-----------|----------|---------|
| `ApplyListFilter(list, selector)` | Filter by list of values (handles null/empty) | `query.ApplyListFilter(filter.Ids, x => x.Id)` |
| `ApplyStringNullableValueFilter(value, selector, operation)` | String filtering (Equals/Contains/StartsWith/EndsWith) | `query.ApplyStringNullableValueFilter(filter.Name, x => x.Name, StringFilterOperation.Contains)` |
| `ApplyComparableRangeFilter(range, selector)` | Date/number ranges with inclusive/exclusive bounds | `query.ApplyComparableRangeFilter(filter.PriceRange, x => x.Price)` |
| `ApplyNullableValueFilter(value, selector)` | Filter nullable primitives | `query.ApplyNullableValueFilter(filter.IsActive, x => x.IsActive)` |
| `ApplyNavigationNullableFilter(list, nav, selector)` | Filter by related entities | `query.ApplyNavigationNullableFilter(filter.TagIds, x => x.Tags, t => t.Id)` |

**Example using all extensions:**

```csharp
protected override IQueryable<ProductDal> ApplyFilter(
    IQueryable<ProductDal> query,
    ProductFilter filter)
{
    // List filtering
    query = query.ApplyListFilter(filter.Ids, x => x.Id);
    query = query.ApplyListFilter(filter.CategoryIds, x => x.CategoryId);

    // String filtering
    query = query.ApplyStringNullableValueFilter(
        filter.Name, x => x.Name, StringFilterOperation.Contains);
    query = query.ApplyStringNullableValueFilter(
        filter.Sku, x => x.Sku, StringFilterOperation.Equals);

    // Range filtering
    query = query.ApplyComparableRangeFilter(filter.PriceRange, x => x.Price);
    query = query.ApplyComparableRangeFilter(filter.CreatedRange, x => x.Created);

    // Boolean filtering
    query = query.ApplyNullableValueFilter(filter.IsActive, x => x.IsActive);

    // Navigation filtering (many-to-many)
    query = query.ApplyNavigationNullableFilter(
        filter.TagIds, x => x.ProductTags, pt => pt.TagId);

    return query;
}
```

## Features

### 🔢 ID-Based Repositories

Use `BaseIdRepository` for entities with numeric ID primary keys.

```csharp
public interface IRepository<TEntity, TFilter, TEdit>
{
    Task<int> Count(TFilter filter);
    Task<IReadOnlyList<TEntity>> Get(TFilter filter);
    Task<TEntity?> Get(long id);
    Task<IReadOnlyList<TEntity>> Get(IReadOnlyList<long> ids);
    Task<TEntity> Create(TEdit edit);
    Task<IReadOnlyList<long>> Create(IReadOnlyList<TEdit> edits);
    Task<TEntity> Update(long id, TEdit edit);
    Task<TEntity> Update(TEntity entity);
    Task Delete(long id);
    Task Delete(IReadOnlyList<long> ids);
}
```

### 🔑 Key-Based Repositories

Use `BaseKeyRepository` for entities with string key primary keys.

```csharp
public class ApiKey : IKeyEntity
{
    public string Key { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset Updated { get; init; }
}

public class ApiKeyRepository :
    BaseKeyRepository<AppDbContext, ApiKeyDal, ApiKey, ApiKeyFilter, ApiKeyEdit>
{
    // Same pattern as ID-based repositories
}
```

### 📄 Built-In Pagination

Add pagination to any repository by extending `BaseIdPaginationRepository`:

```csharp
public class ProductRepository :
    BaseIdPaginationRepository<AppDbContext, ProductDal, Product, ProductFilter, ProductEdit>
{
    // Now you have access to paginated Get method
}

// Use it
var result = await repository.Get(
    new PaginationRequest(page: 1, pageSize: 20),
    filter);

Console.WriteLine($"Total: {result.TotalCount}");
Console.WriteLine($"Pages: {result.TotalPages}");
foreach (var product in result.Items)
{
    Console.WriteLine(product.Name);
}
```

**PaginationRequest** validates page numbers and page sizes automatically:
- Minimum page: 1
- Minimum page size: 1

### ⏰ Automatic Timestamps

The `Created` and `Updated` fields are managed automatically:

```csharp
var product = await repository.Create(edit);
// Created and Updated are set to current time

await Task.Delay(1000);

var updated = await repository.Update(product.Id, editChanges);
// Updated is set to new time, Created remains unchanged
```

### 🪝 Customizable Hooks

Inject custom logic at key points in the lifecycle:

```csharp
public class ProductRepository : BaseIdRepository<...>
{
    protected override void AfterDalCreatedHook(
        ProductDal dal,
        ProductEdit edit,
        AppDbContext context)
    {
        // Add related entities, validate business rules, etc.
        dal.Slug = GenerateSlug(dal.Name);
    }

    protected override void AfterDalApplyHook(
        ProductDal dal,
        ProductEdit edit,
        AppDbContext context)
    {
        // Custom logic after updates
        if (dal.Price < 0)
        {
            throw new ValidationException("Price cannot be negative");
        }
    }

    protected override void AfterDalDeleteHook(
        ProductDal dal,
        AppDbContext context)
    {
        // Clean up related data, log deletions, etc.
        logger.LogInformation("Deleted product {ProductId}", dal.Id);
    }
}
```

### 🔗 Managing Sub-Entities (One-to-Many Relationships)

**CRITICAL RULE: Handle sub-entities in repository hooks, NOT in DAL's Apply() method.**

**Example**: Product has many ProductImages

```csharp
// Edit includes sub-entity data
public record ProductImageEdit
{
    public long? Id { get; init; }  // Null for new, value for existing
    public string Url { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public record ProductEdit
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public List<ProductImageEdit> Images { get; init; } = new();
}

// Repository handles sub-entities in hooks
public class ProductRepository(
    IKeyGenService keyGenService,
    IDbContextFactory<AppDbContext> contextFactory,
    IClock clock)
    : BaseIdRepository<AppDbContext, ProductDal, Product, ProductFilter, ProductEdit>(
        contextFactory, clock)
{
    // For CREATE - Add all sub-entities
    protected override void AfterDalCreatedHook(
        ProductDal dal,
        ProductEdit edit,
        AppDbContext context)
    {
        foreach (var imageEdit in edit.Images)
        {
            context.ProductImages.Add(new ProductImageDal
            {
                Id = keyGenService.GetId(),  // Generate Snowflake ID
                ProductId = dal.Id,
                Url = imageEdit.Url,
                SortOrder = imageEdit.SortOrder,
            });
        }
    }

    // For UPDATE - Add/Update/Delete logic
    protected override void AfterDalApplyHook(
        ProductDal dal,
        ProductEdit edit,
        AppDbContext context)
    {
        // Get existing sub-entities
        var existing = context.ProductImages
            .Where(x => x.ProductId == dal.Id)
            .ToList();

        var existingById = existing.ToDictionary(x => x.Id);
        var toKeep = new HashSet<long>();

        foreach (var imageEdit in edit.Images)
        {
            if (imageEdit.Id.HasValue && existingById.ContainsKey(imageEdit.Id.Value))
            {
                // UPDATE existing
                var existingImage = existingById[imageEdit.Id.Value];
                existingImage.Url = imageEdit.Url;
                existingImage.SortOrder = imageEdit.SortOrder;
                toKeep.Add(imageEdit.Id.Value);
            }
            else
            {
                // ADD new
                context.ProductImages.Add(new ProductImageDal
                {
                    Id = keyGenService.GetId(),
                    ProductId = dal.Id,
                    Url = imageEdit.Url,
                    SortOrder = imageEdit.SortOrder,
                });
            }
        }

        // DELETE removed
        var toRemove = existing.Where(x => !toKeep.Contains(x.Id)).ToList();
        if (toRemove.Any())
            context.ProductImages.RemoveRange(toRemove);
    }
}
```

**Sub-Entity Rules:**
- ✅ Handle sub-entities in `AfterDalCreatedHook` (for create)
- ✅ Handle sub-entities in `AfterDalApplyHook` (for update)
- ✅ Use `keyGenService.GetId()` for sub-entity IDs
- ✅ Implement add/update/delete logic in update hook
- ❌ Never handle sub-entities in DAL's `Apply()` method
- ❌ Never use auto-increment for sub-entity IDs

### 🔍 Custom Queries

Override `GetQuery()` to add includes or custom query logic:

```csharp
protected override IQueryable<ProductDal> GetQuery(AppDbContext context)
{
    return context.Products
        .Include(p => p.Category)
        .Include(p => p.Reviews)
        .Where(p => !p.IsDeleted); // Soft delete filter
}
```

### 🎯 Filter Pattern

Create type-safe filter objects for your queries:

```csharp
public record ProductFilter
{
    public string? SearchTerm { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public long? CategoryId { get; init; }
    public bool? InStock { get; init; }
}

protected override IQueryable<ProductDal> ApplyFilter(
    IQueryable<ProductDal> query,
    ProductFilter filter)
{
    if (!string.IsNullOrEmpty(filter.SearchTerm))
    {
        query = query.Where(p =>
            p.Name.Contains(filter.SearchTerm) ||
            p.Description.Contains(filter.SearchTerm));
    }

    if (filter.MinPrice.HasValue)
        query = query.Where(p => p.Price >= filter.MinPrice.Value);

    if (filter.MaxPrice.HasValue)
        query = query.Where(p => p.Price <= filter.MaxPrice.Value);

    if (filter.CategoryId.HasValue)
        query = query.Where(p => p.CategoryId == filter.CategoryId.Value);

    if (filter.InStock.HasValue)
        query = query.Where(p => p.Stock > 0);

    return query;
}
```

### 🔄 DAL Transformation Pattern

The DAL (Data Access Layer) pattern separates database entities from domain entities:

**Benefits:**
- Database schema changes don't affect your domain
- Can transform data on read/write
- Keeps EF Core concerns out of your domain layer

```csharp
public class ProductDal : BaseDal<Product, ProductEdit>
{
    // Database columns with EF Core attributes
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    // Transform to domain entity
    public override Product ToEntity() => new()
    {
        Name = Name,
        // ... map other properties
    };

    // Apply changes from edit object
    public override void Apply(ProductEdit edit)
    {
        Name = edit.Name;
        // ... apply other changes
    }
}
```

## Complete Example

Here's a complete working example with all the pieces:

```csharp
// 1. Domain Entity
public class Product : IIdEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset Updated { get; init; }
}

// 2. Edit DTO
public record ProductEdit(string Name, decimal Price);

// 3. Filter
public record ProductFilter(string? SearchTerm = null, decimal? MinPrice = null);

// 4. DAL
[Table("products")]
public class ProductDal : BaseDal<Product, ProductEdit>, IDalId
{
    [Key, Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }

    public override Product ToEntity() => new()
    {
        Id = Id,
        Name = Name,
        Price = Price,
        Created = Created,
        Updated = Updated
    };

    public override void Apply(ProductEdit edit)
    {
        Name = edit.Name;
        Price = edit.Price;
    }
}

// 5. Repository
public class ProductRepository(
    IDbContextFactory<AppDbContext> contextFactory,
    IClock clock,
    ILogger<ProductRepository> logger)
    : BaseIdPaginationRepository<AppDbContext, ProductDal, Product, ProductFilter, ProductEdit>(
        contextFactory, clock, logger)
{
    protected override IQueryable<ProductDal> ApplyFilter(
        IQueryable<ProductDal> query,
        ProductFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.SearchTerm))
            query = query.Where(p => p.Name.Contains(filter.SearchTerm));

        if (filter.MinPrice.HasValue)
            query = query.Where(p => p.Price >= filter.MinPrice.Value);

        return query;
    }

    protected override DbSet<ProductDal> GetDbSet(AppDbContext context) => context.Products;

    protected override ProductDal CreateDal(ProductEdit edit) => new()
    {
        Name = edit.Name,
        Price = edit.Price
    };

    protected override NotFoundException GetNotFoundException(long id)
        => new NotFoundIdException<Product>(id);
}

// 6. Register and use
services.AddDbContextFactory<AppDbContext>(options => ...);
services.AddSingleton<IClock, DateTimeClock>();
services.AddScoped<ProductRepository>();
```

## Creating New Repository Checklist

Follow this step-by-step guide when creating a new repository:

### 1. Define Entities

```csharp
// Edit record - mutable properties
public record ProductEdit
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public long CategoryId { get; init; }
}

// Entity record - inherits Edit, adds Id and timestamps
public record Product : ProductEdit, IMagicEntity, IIdEntity
{
    public long Id { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset Updated { get; init; }
}

// Filter class - query criteria
public record ProductFilter
{
    public List<long>? Ids { get; init; }
    public List<long>? CategoryIds { get; init; }
    public string? Name { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
}
```

### 2. Create DAL Class

```csharp
[Table("products")]                                  // ✅ snake_case
[Index(nameof(CategoryId))]                          // ✅ Index FKs
[Index(nameof(Name))]                                // ✅ Index queries
public class ProductDal : IDalTransform<Product, ProductEdit>, IDalId, IDal
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // ✅ Snowflake IDs
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [Column("name")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("price")]
    [Precision(18, 2)]
    public decimal Price { get; set; }

    [Required]
    [Column("category_id")]
    public long CategoryId { get; set; }

    [Required]
    [Column("created")]
    public DateTimeOffset Created { get; set; }

    [Required]
    [Column("updated")]
    public DateTimeOffset Updated { get; set; }

    // Implement required methods
    public Product ToEntity() => new()
    {
        Id = Id,
        Name = Name,
        Price = Price,
        CategoryId = CategoryId,
        Created = Created,
        Updated = Updated
    };

    public void Apply(ProductEdit edit)
    {
        Name = edit.Name;
        Price = edit.Price;
        CategoryId = edit.CategoryId;
    }

    public static ProductDal From(ProductEdit edit, long id)
    {
        var dal = new ProductDal { Id = id };
        dal.Apply(edit);
        return dal;
    }
}
```

### 3. Create Repository Interface

```csharp
public interface IProductRepository : IRepository<Product, ProductFilter, ProductEdit>
{
    // Add custom methods if needed (99% won't need any)
}
```

### 4. Create Repository Class

```csharp
public class ProductRepository(
    IKeyGenService keyGenService,
    IDbContextFactory<AppDbContext> contextFactory,
    IClock clock)
    : BaseIdRepository<AppDbContext, ProductDal, Product, ProductFilter, ProductEdit>(
        contextFactory, clock),
      IProductRepository
{
    protected override ProductDal CreateDal(ProductEdit edit)
        => ProductDal.From(edit, keyGenService.GetId());

    protected override DbSet<ProductDal> GetDbSet(AppDbContext context)
        => context.Products;

    protected override NotFoundException GetNotFoundException(long id)
        => new NotFoundIdException(id, nameof(Product));

    protected override IQueryable<ProductDal> ApplyFilter(
        IQueryable<ProductDal> query,
        ProductFilter filter)
    {
        query = query.ApplyListFilter(filter.Ids, x => x.Id);
        query = query.ApplyListFilter(filter.CategoryIds, x => x.CategoryId);
        query = query.ApplyStringNullableValueFilter(
            filter.Name, x => x.Name, StringFilterOperation.Contains);

        if (filter.MinPrice.HasValue)
            query = query.Where(x => x.Price >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query = query.Where(x => x.Price <= filter.MaxPrice.Value);

        return query;
    }

    // Optional: For eager loading
    protected override IQueryable<ProductDal> GetQuery(AppDbContext context)
    {
        return context.Products.Include(x => x.Category);
    }

    // Optional: Add hooks for sub-entities or business logic
}
```

### 5. Add DbSet to DbContext

```csharp
public class AppDbContext : DbContext
{
    public DbSet<ProductDal> Products { get; set; } = null!;

    // DO NOT configure entities in OnModelCreating
    // All configuration should be in DAL annotations!
}
```

### 6. Register Repository

```csharp
// In your dependency injection setup
services.AddScoped<IProductRepository, ProductRepository>();
```

### 7. Create and Apply Migration

```bash
# Create migration
dotnet ef migrations add AddProduct

# Apply to database
dotnet ef database update
```

## Key Rules Summary

### ✅ DO

- Use snake_case for table and column names
- Use Snowflake IDs via `keyGenService.GetId()`
- Configure database structure with DAL annotations
- Use filter extensions (`ApplyListFilter`, etc.)
- Handle sub-entities in repository hooks
- Use `Get(filter)` for 99% of queries
- Add indexes on foreign keys and queried fields
- Make Entity inherit from Edit
- Use `[DatabaseGenerated(DatabaseGeneratedOption.None)]`

### ❌ DON'T

- Use auto-increment IDs
- Configure tables in `DbContext.OnModelCreating()`
- Handle sub-entities in DAL's `Apply()` method
- Create custom query methods for simple queries
- Generate IDs manually or with `Guid.NewGuid()`
- Use camelCase or PascalCase for database names
- Forget to add indexes on foreign keys

## Snowflake ID Generator

MagicCSharp.Data includes a Snowflake ID generator for creating globally, distributed safe, unique, time-sortable IDs.

### Why Snowflake IDs?

- ✅ **Sortable by time** - Natural chronological ordering
- ✅ **Globally unique** - No collisions across distributed systems
- ✅ **High performance** - 4096 IDs per millisecond per generator
- ✅ **Database friendly** - 64-bit integers, not GUIDs
- ✅ **Distributed systems** - 1024 generators can run in parallel
- ❌ **Never use auto-increment** - Causes issues in distributed systems
- ❌ **Never use Guid.NewGuid()** - Not sortable, poor performance

### Registration

```csharp
// In your Startup.cs or Program.cs
services.RegisterSnowflakeKeyGen();

// For distributed systems: Specify unique generator ID (0-1023)
// Each instance/server should use a different ID
services.RegisterSnowflakeKeyGen(generatorId: 1);  // Server 1
services.RegisterSnowflakeKeyGen(generatorId: 2);  // Server 2
```

### ID Structure

Snowflake IDs are 64-bit integers composed of:
- **41 bits** - Timestamp in milliseconds (69 years of IDs)
- **10 bits** - Generator ID (1024 parallel generators)
- **12 bits** - Sequence number (4096 IDs per millisecond)
- **1 bit** - Unused sign bit

This guarantees uniqueness across distributed systems and natural time-based sorting.

### Example Usage

```csharp
public class ProductRepository(
    IKeyGenService keyGenService,  // ✅ Inject the service
    IDbContextFactory<AppDbContext> contextFactory,
    IClock clock)
    : BaseIdRepository<AppDbContext, ProductDal, Product, ProductFilter, ProductEdit>(
        contextFactory, clock)
{
    protected override ProductDal CreateDal(ProductEdit edit)
        => ProductDal.From(edit, keyGenService.GetId());  // ✅ Generate Snowflake ID
}
```

## Related Packages

**[MagicCSharp](https://www.nuget.org/packages/MagicCSharp)** - Core infrastructure library (required)
**[MagicCSharp.Events](https://www.nuget.org/packages/MagicCSharp.Events)** - Event-driven architecture

## License

MIT License - See LICENSE file for details.
