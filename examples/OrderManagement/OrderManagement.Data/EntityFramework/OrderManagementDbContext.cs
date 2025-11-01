using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OrderManagement.Data.EntityFramework.DALs;

namespace OrderManagement.Data.EntityFramework;

public class OrderManagementDbContext(DbContextOptions<OrderManagementDbContext> options) : DbContext(options)
{
    public DbSet<OrderDal> Orders { get; set; }
    public DbSet<OrderItemDal> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Global configuration: Convert all enums to strings in the database
        // This makes enums more robust to changes and human-readable
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var underlyingType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (underlyingType.IsEnum)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasConversion<string>()
                        .HasColumnType("VARCHAR(100)");
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
///     This is used by the CLI tool to generate migrations.
/// </summary>
public class MagicContextFactory : IDesignTimeDbContextFactory<OrderManagementDbContext>
{
    public OrderManagementDbContext CreateDbContext(string[] args)
    {
        // Creating the connection string for the CLI tool.
        var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
        if (string.IsNullOrWhiteSpace(dbHost))
        {
            Console.WriteLine("DB_HOST environment variable is not set. Defaulting to localhost");
            dbHost = "localhost";
        }

        Console.WriteLine("Using DB_HOST: {0}", dbHost);
        var dbPort = Environment.GetEnvironmentVariable("DB_PORT");
        if (string.IsNullOrWhiteSpace(dbPort))
        {
            Console.WriteLine("DB_PORT environment variable is not set. Defaulting to 3306");
            dbPort = "5432";
        }

        Console.WriteLine("Using DB_PORT: {0}", dbPort);
        var dbName = Environment.GetEnvironmentVariable("DB_NAME");
        if (string.IsNullOrWhiteSpace(dbName))
        {
            Console.WriteLine("DB_NAME environment variable is not set. Defaulting to magicdoor");
            dbName = "order_management";
        }

        Console.WriteLine("Using DB_NAME: {0}", dbName);
        var dbUser = Environment.GetEnvironmentVariable("DB_USER");
        if (string.IsNullOrWhiteSpace(dbUser))
        {
            Console.WriteLine("DB_USER environment variable is not set. Defaulting to root");
            dbUser = "postgres";
        }

        Console.WriteLine("Using DB_USER: {0}", dbUser);
        var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(dbPass))
        {
            Console.WriteLine("DB_PASSWORD environment variable is not set. Defaulting to 12345");
            dbPass = "12345";
        }

        Console.WriteLine("Using dbPass: {0}", !string.IsNullOrWhiteSpace(dbPass) ? "********" : "Null");

        var optionsBuilder = new DbContextOptionsBuilder<OrderManagementDbContext>();
        optionsBuilder.UseNpgsql($"Host={dbHost};Port={dbPort};Username={dbUser};Password={dbPass};Database={dbName}");

        return new OrderManagementDbContext(optionsBuilder.Options);
    }
}