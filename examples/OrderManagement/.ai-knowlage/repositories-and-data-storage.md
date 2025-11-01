# Repositories & Data Storage - Quick Reference

## Core Pattern

**BaseIdRepository** provides CRUD with filtering, batch operations, and automatic timestamping.

```csharp
public class MyEntityEfRepository(
    IKeyGenService keyGenService,
    IDbContextFactory<RevocoDbContext> contextFactory,
    IClock clock)
    : BaseIdRepository<RevocoDbContext, MyEntityDal, MyEntity, MyEntityFilter, MyEntityEdit>(
        contextFactory, clock), IMyEntityRepository
```

## DAL Pattern (Data Access Layer)

### Required Structure
- **DAL Class**: Database entity with EF annotations
- **Entity Record**: Business object (inherits from Edit)
- **Edit Record**: Mutable properties
- **Filter Class**: Query criteria

### DAL Required Methods

```csharp
public class MyEntityDal : IDalTransform<MyEntity, MyEntityEdit>, IDalId, IDal
{
    // 1. Convert DAL → Entity
    public MyEntity ToEntity() { }

    // 2. Update DAL from Edit
    public void Apply(MyEntityEdit edit) { }

    // 3. Create DAL from Edit
    public static MyEntityDal From(MyEntityEdit edit, long id)
    {
        var dal = new MyEntityDal { Id = id };
        dal.Apply(edit);
        return dal;
    }
}
```

### DAL Annotations (Use These, Not DbContext Config)

```csharp
[Table("my_entities")]                    // Snake case table name
[Index(nameof(UserId))]                   // Add indexes for queries
[Index(nameof(ExternalId))]
public class MyEntityDal {
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // Use Snowflake IDs
    [Column("id")]
    public long Id { get; set; }

    [Required]                            // NOT NULL
    [Column("user_id")]
    [StringLength(100)]                   // VARCHAR length
    public string UserId { get; set; }

    public DateTimeOffset Created { get; set; }  // Automatic timestamps
    public DateTimeOffset Updated { get; set; }
}
```

## Filters & Query Extensions

### Filter Class Pattern
```csharp
public class MyEntityFilter
{
    public List<long>? Ids { get; set; }
    public List<string>? UserIds { get; set; }
    public DateTimeOffset? CreatedAfter { get; set; }
    public string? Name { get; set; }
}
```

### Apply Filter Pattern
```csharp
protected override IQueryable<MyEntityDal> ApplyFilter(
    IQueryable<MyEntityDal> query, MyEntityFilter filter)
{
    query = query.ApplyListFilter(filter.Ids, x => x.Id);
    query = query.ApplyListFilter(filter.UserIds, x => x.UserId);
    query = query.ApplyStringNullableValueFilter(filter.Name, x => x.Name,
        StringFilterOperation.Contains);

    if (filter.CreatedAfter.HasValue)
        query = query.Where(x => x.Created >= filter.CreatedAfter.Value);

    return query;
}
```

### Available Filter Extensions

| Extension | Use Case |
|-----------|----------|
| `ApplyListFilter(list, selector)` | Filter by list of values (handles null/empty) |
| `ApplyStringNullableValueFilter(value, selector, operation)` | String filtering (Equals/Contains/StartsWith/EndsWith) |
| `ApplyComparableRangeFilter(range, selector)` | Date/number ranges with inclusive/exclusive bounds |
| `ApplyNullableValueFilter(value, selector)` | Filter nullable primitives |
| `ApplyNavigationNullableFilter(list, nav, selector)` | Filter by related entities |

## Query Strategy: Always Use Get with Filters

**CRITICAL: 99% of queries should use the `Get(filter)` method. Only create custom query methods when absolutely necessary.**

### Correct Pattern (Use This)
```csharp
// ✅ Use Get with filter - Handles 99% of queries
var events = await repository.Get(new CalendarEventFilter
{
    UserIds = [userId],
    IntegrationProviderIds = [integrationId],
    StartTimeAfter = DateTimeOffset.UtcNow,
});

// ✅ Single result - Get returns list, take first
var latestEvent = (await repository.Get(new CalendarEventFilter
{
    UserIds = [userId],
    Limit = 1,
    OrderBy = OrderByDirection.Descending,
})).FirstOrDefault();
```

### Wrong Pattern (Avoid This)
```csharp
// ❌ Don't create custom methods for simple queries
public async Task<List<CalendarEvent>> GetByUserAndIntegration(string userId, long integrationId)
{
    // This should use Get with filter instead!
}

// ❌ Don't create custom methods for single results
public async Task<CalendarEvent?> GetLatestByUser(string userId)
{
    // This should use Get with filter + FirstOrDefault instead!
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
public async Task<Dictionary<string, int>> GetEventCountByProvider(string userId)
{
    // Custom implementation needed for complex aggregation
}
```

### Key Rule
**If you can express the query with filter properties → Use Get with filter. Always.**

## Repository Required Overrides

```csharp
protected override MyEntityDal CreateDal(MyEntityEdit edit)
{
    return MyEntityDal.From(edit, keyGenService.GetId());  // Use Snowflake IDs
}

protected override DbSet<MyEntityDal> GetDbSet(RevocoDbContext context)
{
    return context.MyEntities;
}

protected override NotFoundException GetNotFoundException(long id)
{
    return new NotFoundIdException(id, nameof(MyEntity));
}

// Optional: For eager loading
protected override IQueryable<MyEntityDal> GetQuery(RevocoDbContext context)
{
    return context.MyEntities.Include(x => x.RelatedEntities);
}
```

## Managing Sub-Entities (One-to-Many Relationships)

**Rule**: Handle sub-entities in repository hooks, NOT in DAL's Apply() method.

### Pattern: Add/Update/Delete Logic

```csharp
protected override void AfterDalCreatedHook(MyEntityDal dal, MyEntityEdit edit, RevocoDbContext context)
{
    // For new records: Add all sub-entities
    foreach (var subEdit in edit.SubEntities)
    {
        context.SubEntities.Add(new SubEntityDal
        {
            Id = keyGenService.GetId(),  // Generate Snowflake ID
            ParentId = dal.Id,
            // ... other properties
        });
    }
}

protected override void AfterDalApplyHook(MyEntityDal dal, MyEntityEdit edit, RevocoDbContext context)
{
    // For updates: Perform add/update/delete
    var existing = context.SubEntities.Where(x => x.ParentId == dal.Id).ToList();
    var existingById = existing.ToDictionary(x => x.Id);
    var toKeep = new HashSet<long>();

    foreach (var subEdit in edit.SubEntities)
    {
        if (subEdit.Id.HasValue && existingById.ContainsKey(subEdit.Id.Value))
        {
            // Update existing
            var existingSub = existingById[subEdit.Id.Value];
            existingSub.Apply(subEdit);
            toKeep.Add(subEdit.Id.Value);
        }
        else
        {
            // Add new
            context.SubEntities.Add(new SubEntityDal
            {
                Id = keyGenService.GetId(),
                ParentId = dal.Id,
                // ... other properties
            });
        }
    }

    // Delete removed
    var toRemove = existing.Where(x => !toKeep.Contains(x.Id)).ToList();
    if (toRemove.Any())
        context.SubEntities.RemoveRange(toRemove);
}
```

## Creating New Repository Checklist

1. **Define entities** (Edit record, Entity record, Filter class)
2. **Create DAL class** with:
   - `[Table]` and `[Column]` annotations (snake_case)
   - `[Index]` on queried fields
   - `ToEntity()`, `Apply()`, `From()` methods
3. **Create repository interface** extending `IRepository<TEntity, TFilter, TEdit>`
4. **Create repository class** extending `BaseIdRepository`
5. **Override required methods**:
   - `CreateDal()` - Use `keyGenService.GetId()`
   - `GetDbSet()` - Return DbSet from context
   - `GetNotFoundException()` - Return exception
   - `ApplyFilter()` - Implement query filters
6. **Add hooks if needed** (sub-entities, custom logic)
7. **Add DbSet to RevocoDbContext**:
   ```csharp
   public DbSet<MyEntityDal> MyEntities { get; set; }
   ```
8. **Register in RepositoryModule.cs**:
   ```csharp
   services.AddScoped<IMyEntityRepository, MyEntityEfRepository>();
   ```
9. **Create and apply migration**:
   ```bash
   dotnet ef migrations add AddMyEntity
   dotnet ef database update
   ```

## Key Rules

- ✅ **Always use Snowflake IDs** via `keyGenService.GetId()`
- ✅ **Configure database in DAL annotations**, not DbContext
- ✅ **Use filter extensions** for consistent query patterns
- ✅ **Handle sub-entities in hooks**, not Apply()
- ✅ **Use snake_case** for table/column names
- ✅ **Add indexes** on foreign keys and queried fields
- ❌ **Never use auto-increment IDs**
- ❌ **Never store sub-entity logic in DAL Apply()**
- ❌ **Never configure tables in DbContext OnModelCreating**
