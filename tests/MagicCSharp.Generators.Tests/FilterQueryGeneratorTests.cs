using MagicCSharp.Data.EntityFramework.Dals;
using MagicCSharp.Data.Repositories;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Generators.Tests;

public class FilterQueryGeneratorTests
{
    private const string RepositoryHintName = "Acme.Shipping.Data.ShipmentsRepository.FilterQuery.g.cs";

    private const string Usings = """
                                  using System;
                                  using System.Collections.Generic;
                                  using System.Linq;
                                  using MagicCSharp.Data.EntityFramework.Dals;
                                  using MagicCSharp.Data.EntityFramework.Repositories;
                                  using MagicCSharp.Infrastructure;
                                  using MagicCSharp.Infrastructure.Entities;
                                  using Microsoft.EntityFrameworkCore;

                                  """;

    // A shipment with every column kind the rules cover: an id, a tenant, an enum, a nullable foreign id, a string,
    // a bool, nullable timestamps and the created time.
    private const string ShipmentTypes = """
                                         namespace Acme.Shipping.Data;

                                         public class ShippingContext(DbContextOptions options) : DbContext(options);

                                         public enum ShipmentStatus
                                         {
                                             Booked,
                                             Delivered,
                                         }

                                         public record ShipmentEdit
                                         {
                                             public required long TenantId { get; init; }
                                             public required ShipmentStatus Status { get; init; }
                                             public long? CarrierId { get; init; }
                                             public required string TrackingCode { get; init; }
                                             public required bool IsFragile { get; init; }
                                             public DateTimeOffset? Delivered { get; init; }
                                         }

                                         public record Shipment : ShipmentEdit, IIdEntity
                                         {
                                             public required long Id { get; init; }
                                             public required DateTimeOffset Created { get; init; }
                                             public required DateTimeOffset Updated { get; init; }
                                         }

                                         public class ShipmentDal : BaseIdDal<Shipment, ShipmentEdit>
                                         {
                                             public long TenantId { get; set; }
                                             public ShipmentStatus Status { get; set; }
                                             public long? CarrierId { get; set; }
                                             public string TrackingCode { get; set; } = "";
                                             public bool IsFragile { get; set; }
                                             public DateTimeOffset? Delivered { get; set; }

                                             public override Shipment ToEntity()
                                             {
                                                 return new Shipment
                                                 {
                                                     Id = Id,
                                                     Created = Created,
                                                     Updated = Updated,
                                                     TenantId = TenantId,
                                                     Status = Status,
                                                     CarrierId = CarrierId,
                                                     TrackingCode = TrackingCode,
                                                     IsFragile = IsFragile,
                                                     Delivered = Delivered,
                                                 };
                                             }

                                             public override void Apply(ShipmentEdit edit)
                                             {
                                                 Status = edit.Status;
                                             }
                                         }


                                         """;

    private const string ShipmentsRepository = """

                                               [GenerateFilterQuery]
                                               public partial class ShipmentsRepository(RepositoryServices<ShippingContext> repositoryServices)
                                                   : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices);
                                               """;

    private static readonly MetadataReference[] DataReferences =
    [
        MetadataReference.CreateFromFile(typeof(BaseIdDal<,>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(IRepository<,,,>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ILoggerFactory).Assembly.Location),
    ];

    [Fact]
    public void FilterQuery_WritesApplyFilter_WithOneHelperCallPerFieldInFilterOrder()
    {
        // Arrange
        const string source = Usings + ShipmentTypes + """
                                                       public class ShipmentFilter
                                                       {
                                                           public List<long>? Ids { get; init; }
                                                           public long? TenantId { get; init; }
                                                           public List<ShipmentStatus>? Statuses { get; init; }
                                                           public List<long>? CarrierIds { get; init; }
                                                           public bool? IsDelivered { get; init; }
                                                           public ComparableRange<DateTimeOffset>? Created { get; init; }
                                                       }
                                                       """ + ShipmentsRepository;

        // Act
        var generatorRunResult = RunClean(source);

        // Assert
        Assert.Equal("""
                     // <auto-generated/>
                     #nullable enable

                     namespace Acme.Shipping.Data;

                     partial class ShipmentsRepository
                     {
                         protected override global::System.Linq.IQueryable<global::Acme.Shipping.Data.ShipmentDal> ApplyFilter(global::System.Linq.IQueryable<global::Acme.Shipping.Data.ShipmentDal> query, global::Acme.Shipping.Data.ShipmentFilter filter)
                         {
                             query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyListFilter<global::Acme.Shipping.Data.ShipmentDal, long>(query, filter.Ids, row => row.Id);
                             query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyNullableValueFilter<global::Acme.Shipping.Data.ShipmentDal, long>(query, filter.TenantId, row => row.TenantId);
                             query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyListFilter<global::Acme.Shipping.Data.ShipmentDal, global::Acme.Shipping.Data.ShipmentStatus>(query, filter.Statuses, row => row.Status);
                             query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyListFilter<global::Acme.Shipping.Data.ShipmentDal, long>(query, filter.CarrierIds, row => row.CarrierId);
                             query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyNullablePresenceFilter<global::Acme.Shipping.Data.ShipmentDal, global::System.DateTimeOffset>(query, filter.IsDelivered, row => row.Delivered);
                             query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyComparableRangeFilter<global::Acme.Shipping.Data.ShipmentDal, global::System.DateTimeOffset>(query, filter.Created, row => row.Created);
                             return query;
                         }
                     }

                     """, generatorRunResult.GeneratedText(RepositoryHintName));
    }

    [Fact]
    public void FilterQuery_FiltersTheSingular_OfAnEsPlural()
    {
        // Arrange: 'Statuses' drops 'es'; dropping only 's' would give 'Statuse'.
        var applyFilter = GeneratedApplyFilter("public List<ShipmentStatus>? Statuses { get; init; }");

        // Assert
        Assert.Contains("ApplyListFilter<global::Acme.Shipping.Data.ShipmentDal, global::Acme.Shipping.Data.ShipmentStatus>(query, filter.Statuses, row => row.Status)",
            applyFilter);
    }

    [Fact]
    public void FilterQuery_FiltersTheSingular_OfAnIesPlural()
    {
        // Arrange
        const string source = Usings + """
                                       namespace Acme.Shipping.Data;

                                       public class ShippingContext(DbContextOptions options) : DbContext(options);

                                       public enum ParcelCategory
                                       {
                                           Letter,
                                           Box,
                                       }

                                       public record ParcelEdit
                                       {
                                           public required ParcelCategory Category { get; init; }
                                       }

                                       public record Parcel : ParcelEdit, IIdEntity
                                       {
                                           public required long Id { get; init; }
                                           public required DateTimeOffset Created { get; init; }
                                           public required DateTimeOffset Updated { get; init; }
                                       }

                                       public class ParcelDal : BaseIdDal<Parcel, ParcelEdit>
                                       {
                                           public ParcelCategory Category { get; set; }

                                           public override Parcel ToEntity()
                                           {
                                               return new Parcel { Id = Id, Created = Created, Updated = Updated, Category = Category };
                                           }

                                           public override void Apply(ParcelEdit edit)
                                           {
                                               Category = edit.Category;
                                           }
                                       }

                                       public class ParcelFilter
                                       {
                                           public ParcelCategory[]? Categories { get; init; }
                                       }

                                       [GenerateFilterQuery]
                                       public partial class ParcelsRepository(RepositoryServices<ShippingContext> repositoryServices)
                                           : BaseIdRepository<ShippingContext, ParcelDal, Parcel, ParcelFilter, ParcelEdit>(repositoryServices);
                                       """;

        // Act
        var generatorRunResult = RunClean(source);

        // Assert
        Assert.Contains("(query, filter.Categories, row => row.Category)",
            generatorRunResult.GeneratedText("Acme.Shipping.Data.ParcelsRepository.FilterQuery.g.cs"));
    }

    [Fact]
    public void FilterQuery_FiltersTheSingular_OfAnSPlural()
    {
        // Arrange: 'CarrierIds' drops 's'.
        var applyFilter = GeneratedApplyFilter("public IReadOnlyList<long>? CarrierIds { get; init; }");

        // Assert
        Assert.Contains("ApplyListFilter<global::Acme.Shipping.Data.ShipmentDal, long>(query, filter.CarrierIds, row => row.CarrierId)", applyFilter);
    }

    [Fact]
    public void FilterQuery_FiltersAStringColumn_FromAStringList()
    {
        // Arrange
        var applyFilter = GeneratedApplyFilter("public List<string>? TrackingCodes { get; init; }");

        // Assert
        Assert.Contains("ApplyListFilter<global::Acme.Shipping.Data.ShipmentDal>(query, filter.TrackingCodes, row => row.TrackingCode)", applyFilter);
    }

    [Fact]
    public void FilterQuery_FiltersAnEqualValue_FromANullableFieldOfTheSameName()
    {
        // Arrange
        var applyFilter = GeneratedApplyFilter("public long? TenantId { get; init; }");

        // Assert
        Assert.Contains("ApplyNullableValueFilter<global::Acme.Shipping.Data.ShipmentDal, long>(query, filter.TenantId, row => row.TenantId)", applyFilter);
    }

    [Fact]
    public void FilterQuery_FiltersABoolColumn_FromABoolFieldOfTheSameName()
    {
        // Arrange: the entity has 'IsFragile', so the field filters it by value rather than as a presence check.
        var applyFilter = GeneratedApplyFilter("public bool? IsFragile { get; init; }");

        // Assert
        Assert.Contains("ApplyNullableValueFilter<global::Acme.Shipping.Data.ShipmentDal, bool>(query, filter.IsFragile, row => row.IsFragile)", applyFilter);
    }

    [Fact]
    public void FilterQuery_FiltersPresence_FromAnIsFieldOverANullableColumn()
    {
        // Arrange
        var applyFilter = GeneratedApplyFilter("public bool? IsDelivered { get; init; }");

        // Assert
        Assert.Contains("ApplyNullablePresenceFilter<global::Acme.Shipping.Data.ShipmentDal, global::System.DateTimeOffset>(query, filter.IsDelivered, row => row.Delivered)",
            applyFilter);
    }

    [Fact]
    public void FilterQuery_FiltersARange_FromAFieldNamedAfterTheColumn()
    {
        // Arrange
        var applyFilter = GeneratedApplyFilter("public ComparableRange<DateTimeOffset>? Created { get; init; }");

        // Assert
        Assert.Contains("ApplyComparableRangeFilter<global::Acme.Shipping.Data.ShipmentDal, global::System.DateTimeOffset>(query, filter.Created, row => row.Created)",
            applyFilter);
    }

    [Fact]
    public void FilterQuery_FiltersARange_FromAFieldNamedRange()
    {
        // Arrange: a nullable column takes a range of its value type.
        var applyFilter = GeneratedApplyFilter("public ComparableRange<DateTimeOffset>? DeliveredRange { get; init; }");

        // Assert
        Assert.Contains("ApplyComparableRangeFilter<global::Acme.Shipping.Data.ShipmentDal, global::System.DateTimeOffset>(query, filter.DeliveredRange, row => row.Delivered)",
            applyFilter);
    }

    [Fact]
    public void FilterQuery_FiltersAFieldTheFilterInherits()
    {
        // Arrange
        const string source = Usings + ShipmentTypes + """
                                                       public class TenantFilter
                                                       {
                                                           public long? TenantId { get; init; }
                                                       }

                                                       public class ShipmentFilter : TenantFilter
                                                       {
                                                           public List<long>? Ids { get; init; }
                                                       }
                                                       """ + ShipmentsRepository;

        // Act
        var applyFilter = RunClean(source).GeneratedText(RepositoryHintName);

        // Assert: own fields first, then inherited ones.
        Assert.Contains("""
                            query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyListFilter<global::Acme.Shipping.Data.ShipmentDal, long>(query, filter.Ids, row => row.Id);
                                query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyNullableValueFilter<global::Acme.Shipping.Data.ShipmentDal, long>(query, filter.TenantId, row => row.TenantId);
                        """, applyFilter);
    }

    [Fact]
    public void FilterQuery_CallsApplyCustomFilterLast_WhenItReadsTheCustomField()
    {
        // Arrange
        const string source = Usings + ShipmentTypes + """
                                                       public class ShipmentFilter
                                                       {
                                                           public long? TenantId { get; init; }
                                                           public string? TrackingCodeContains { get; init; }
                                                       }

                                                       [GenerateFilterQuery]
                                                       public partial class ShipmentsRepository(RepositoryServices<ShippingContext> repositoryServices)
                                                           : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices)
                                                       {
                                                           private partial IQueryable<ShipmentDal> ApplyCustomFilter(IQueryable<ShipmentDal> query, ShipmentFilter filter)
                                                           {
                                                               if (filter.TrackingCodeContains == null)
                                                               {
                                                                   return query;
                                                               }

                                                               return query.Where(shipmentDal => shipmentDal.TrackingCode.Contains(filter.TrackingCodeContains));
                                                           }
                                                       }
                                                       """;

        // Act
        var generatorRunResult = RunClean(source);

        // Assert
        Assert.Equal("""
                     // <auto-generated/>
                     #nullable enable

                     namespace Acme.Shipping.Data;

                     partial class ShipmentsRepository
                     {
                         protected override global::System.Linq.IQueryable<global::Acme.Shipping.Data.ShipmentDal> ApplyFilter(global::System.Linq.IQueryable<global::Acme.Shipping.Data.ShipmentDal> query, global::Acme.Shipping.Data.ShipmentFilter filter)
                         {
                             query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyNullableValueFilter<global::Acme.Shipping.Data.ShipmentDal, long>(query, filter.TenantId, row => row.TenantId);
                             query = ApplyCustomFilter(query, filter);
                             return query;
                         }

                         private partial global::System.Linq.IQueryable<global::Acme.Shipping.Data.ShipmentDal> ApplyCustomFilter(global::System.Linq.IQueryable<global::Acme.Shipping.Data.ShipmentDal> query, global::Acme.Shipping.Data.ShipmentFilter filter);
                     }

                     """, generatorRunResult.GeneratedText(RepositoryHintName));
    }

    [Fact]
    public void FilterQuery_ReportsMCS1301_WhenNoApplyCustomFilterIsWritten()
    {
        // Arrange
        const string source = Usings + ShipmentTypes + """
                                                       public class ShipmentFilter
                                                       {
                                                           public string? TrackingCodeContains { get; init; }
                                                       }
                                                       """ + ShipmentsRepository;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1301 ", diagnostic);
        Assert.Contains("'ShipmentFilter.TrackingCodeContains' matches no property of 'Shipment' by the filter name rules, and " +
                        "'ShipmentsRepository.ApplyCustomFilter' does not read 'filter.TrackingCodeContains'", diagnostic);
    }

    [Fact]
    public void FilterQuery_TreatsAListOfAnotherValueTypeAsACustomField()
    {
        // Arrange: 'Ids' fits the list rule by name, but 'Id' is a long, not an int.
        const string source = Usings + ShipmentTypes + """
                                                       public class ShipmentFilter
                                                       {
                                                           public List<int>? Ids { get; init; }
                                                       }
                                                       """ + ShipmentsRepository;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1301 ", diagnostic);
        Assert.Contains("'ShipmentFilter.Ids' matches no property of 'Shipment' by the filter name rules", diagnostic);
    }

    [Fact]
    public void FilterQuery_TreatsARangeOfAnotherValueTypeAsACustomField()
    {
        // Arrange: 'Created' fits the range rule by name, but 'Created' is a DateTimeOffset, not a DateTime.
        const string source = Usings + ShipmentTypes + """
                                                       public class ShipmentFilter
                                                       {
                                                           public ComparableRange<DateTime>? Created { get; init; }
                                                       }
                                                       """ + ShipmentsRepository;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1301 ", diagnostic);
        Assert.Contains("'ShipmentFilter.Created' matches no property of 'Shipment' by the filter name rules", diagnostic);
    }

    [Fact]
    public void FilterQuery_ReportsMCS1301_WhenApplyCustomFilterDoesNotReadAFieldAddedLater()
    {
        // Arrange: 'Reference' was added after ApplyCustomFilter was written for 'TrackingCodeContains'.
        const string source = Usings + ShipmentTypes + """
                                                       public class ShipmentFilter
                                                       {
                                                           public string? TrackingCodeContains { get; init; }
                                                           public string? Reference { get; init; }
                                                       }

                                                       [GenerateFilterQuery]
                                                       public partial class ShipmentsRepository(RepositoryServices<ShippingContext> repositoryServices)
                                                           : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices)
                                                       {
                                                           private partial IQueryable<ShipmentDal> ApplyCustomFilter(IQueryable<ShipmentDal> query, ShipmentFilter filter)
                                                           {
                                                               if (filter.TrackingCodeContains == null)
                                                               {
                                                                   return query;
                                                               }

                                                               return query.Where(shipmentDal => shipmentDal.TrackingCode.Contains(filter.TrackingCodeContains));
                                                           }
                                                       }
                                                       """;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1301 ", diagnostic);
        Assert.Contains("'ShipmentFilter.Reference' matches no property of 'Shipment'", diagnostic);
    }

    [Fact]
    public void FilterQuery_ReportsMCS1302_WhenTheDalHasNoColumnForTheMatchedProperty()
    {
        // Arrange: the entity has 'Weight', the Dal stores no such column.
        var source = Usings + EntityWithWeight("") + """
                                                              public class ShipmentFilter
                                                              {
                                                                  public decimal? Weight { get; init; }
                                                              }
                                                              """ + ShipmentsRepository;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1302 ", diagnostic);
        Assert.Contains("'ShipmentFilter.Weight' filters 'Shipment.Weight', but 'ShipmentDal' has no public property 'Weight' to query", diagnostic);
    }

    [Fact]
    public void FilterQuery_ReportsMCS1303_WhenTheDalColumnHasAnotherType()
    {
        // Arrange: the entity's 'Weight' is a decimal, the Dal stores it as a double.
        var source = Usings + EntityWithWeight("public double Weight { get; set; }") + """
                                                                                                 public class ShipmentFilter
                                                                                                 {
                                                                                                     public decimal? Weight { get; init; }
                                                                                                 }
                                                                                                 """ + ShipmentsRepository;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1303 ", diagnostic);
        Assert.Contains("'ShipmentFilter.Weight' filters 'Shipment.Weight' (decimal), but 'ShipmentDal.Weight' is double", diagnostic);
    }

    [Fact]
    public void FilterQuery_ReportsMCS1302_EvenWhenApplyCustomFilterReadsTheField()
    {
        // Arrange: the filter's Matches would filter 'Weight' by the rule, so the query may not hand it to custom code.
        var source = Usings + EntityWithWeight("") + """
                                                              public class ShipmentFilter
                                                              {
                                                                  public decimal? Weight { get; init; }
                                                              }

                                                              [GenerateFilterQuery]
                                                              public partial class ShipmentsRepository(RepositoryServices<ShippingContext> repositoryServices)
                                                                  : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices)
                                                              {
                                                                  private IQueryable<ShipmentDal> ApplyCustomFilter(IQueryable<ShipmentDal> query, ShipmentFilter filter)
                                                                  {
                                                                      return filter.Weight == null ? query : query.Where(shipmentDal => shipmentDal.Id > 0);
                                                                  }
                                                              }
                                                              """;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1302 ", diagnostic);
    }

    [Fact]
    public void FilterQuery_ReportsMCS1305_ForANestedPartialRepository()
    {
        // Arrange
        const string source = Usings + ShipmentTypes + """
                                                       public class ShipmentFilter
                                                       {
                                                           public List<long>? Ids { get; init; }
                                                       }

                                                       public static partial class Storage
                                                       {
                                                           [GenerateFilterQuery]
                                                           public partial class ShipmentsRepository(RepositoryServices<ShippingContext> repositoryServices)
                                                               : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices);
                                                       }
                                                       """;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1305 ", diagnostic);
        Assert.Contains("'ShipmentsRepository' is nested in another type", diagnostic);
    }

    [Fact]
    public void FilterQuery_ReportsMCS1306_ForAGenericPartialRepository()
    {
        // Arrange
        const string source = Usings + ShipmentTypes + """
                                                       public class ShipmentFilter
                                                       {
                                                           public List<long>? Ids { get; init; }
                                                       }

                                                       [GenerateFilterQuery]
                                                       public partial class ShipmentsRepository<TMarker>(RepositoryServices<ShippingContext> repositoryServices)
                                                           : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices);
                                                       """;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1306 ", diagnostic);
        Assert.Contains("'ShipmentsRepository' is generic", diagnostic);
    }

    [Fact]
    public void FilterQuery_GeneratesNothing_ForARepositoryThatWritesApplyFilter()
    {
        // Arrange: a partial repository with its own ApplyFilter, beside one without, which proves the generator ran.
        const string source = Usings + ShipmentTypes + ShipmentFilterWithIds + ControlRepository + """

                                                                                                   [GenerateFilterQuery]
                                                                                                   public partial class ShipmentsRepository(RepositoryServices<ShippingContext> repositoryServices)
                                                                                                       : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices)
                                                                                                   {
                                                                                                       protected override IQueryable<ShipmentDal> ApplyFilter(IQueryable<ShipmentDal> query, ShipmentFilter filter)
                                                                                                       {
                                                                                                           return query;
                                                                                                       }
                                                                                                   }
                                                                                                   """;

        // Act
        var generatorRunResult = RunClean(source);

        // Assert
        var generatedFile = Assert.Single(generatorRunResult.GeneratedFiles);
        Assert.Equal(ControlRepositoryHintName, generatedFile.Path);
    }

    [Fact]
    public void FilterQuery_GeneratesNothing_ForAPartialRepositoryWithoutTheAttribute()
    {
        // Arrange: partial and without ApplyFilter, but not marked; it does not compile for want of ApplyFilter.
        var source = Usings + ShipmentTypes + ShipmentFilterWithIds + ControlRepository + RepositoryWithoutApplyFilter("public partial class");

        // Act
        var generatedHintNames = GeneratedHintNames(source);

        // Assert
        Assert.Equal([ControlRepositoryHintName], generatedHintNames);
    }

    [Fact]
    public void FilterQuery_GeneratesApplyFilter_ForTheRepositoryTheUnmarkedTestLeavesOut()
    {
        // Arrange: the same repository as the unmarked case, with the attribute.
        var source = Usings + ShipmentTypes + ShipmentFilterWithIds + ControlRepository +
                     RepositoryWithoutApplyFilter("[GenerateFilterQuery]\npublic partial class");

        // Act
        var generatedHintNames = RunClean(source).GeneratedFiles.Select(generatedFile => generatedFile.Path).Order().ToArray();

        // Assert
        Assert.Equal([ControlRepositoryHintName, RepositoryHintName], generatedHintNames);
    }

    [Fact]
    public void FilterQuery_ReportsMCS1309_ForAMarkedRepositoryThatIsNotPartial()
    {
        // Arrange
        var source = Usings + ShipmentTypes + ShipmentFilterWithIds + RepositoryWithoutApplyFilter("[GenerateFilterQuery]\npublic class");

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MCS1309 Subject.cs:73 'ShipmentsRepository' has [GenerateFilterQuery] but is not partial; add 'partial' so the generated ApplyFilter " +
                     "can be attached to it", diagnostic);
    }

    [Fact]
    public void FilterQuery_ReportsMCS1310_ForAMarkedAbstractRepositoryBase()
    {
        // Arrange: an abstract base leaves ApplyFilter to the repositories deriving from it, which carry the attribute.
        const string source = Usings + ShipmentTypes + ShipmentFilterWithIds + """

                                                                               [GenerateFilterQuery]
                                                                               public abstract partial class AuditedShipmentsRepository(RepositoryServices<ShippingContext> repositoryServices)
                                                                                   : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices);
                                                                               """;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1310 ", diagnostic);
        Assert.Contains("'AuditedShipmentsRepository' has [GenerateFilterQuery] but is not a concrete class inheriting an abstract ApplyFilter", diagnostic);
    }

    [Fact]
    public void FilterQuery_ReportsMCS1310_ForAMarkedConcreteClassWithoutApplyFilter()
    {
        // Arrange: the attribute put on a report class beside the repository.
        const string source = Usings + ShipmentTypes + ShipmentFilterWithIds + """

                                                                               [GenerateFilterQuery]
                                                                               public partial class ShipmentsReport
                                                                               {
                                                                                   public int Count { get; init; }
                                                                               }
                                                                               """;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.StartsWith("MCS1310 ", diagnostic);
        Assert.Contains("'ShipmentsReport' has [GenerateFilterQuery] but is not a concrete class inheriting an abstract ApplyFilter", diagnostic);
    }

    [Fact]
    public void FilterQuery_ReportsNothing_WhenTheRepositorysBaseDoesNotResolve()
    {
        // Arrange: the compiler reports the missing base; the generator cannot tell whether it declares ApplyFilter.
        const string source = Usings + ShipmentTypes + ShipmentFilterWithIds + """

                                                                               [GenerateFilterQuery]
                                                                               public partial class ShipmentsRepository : MissingRepositoryBase
                                                                               {
                                                                               }
                                                                               """;

        // Act
        var diagnostics = Diagnostics(source);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void FilterQuery_GeneratesNothing_ForAnotherNamespacesAttributeOfTheSameName()
    {
        // Arrange: a codebase's own GenerateFilterQuery attribute is not MagicCSharp's.
        const string toolsAttribute = """
                                      public static class Tools
                                      {
                                          [AttributeUsage(AttributeTargets.Class)]
                                          public sealed class GenerateFilterQueryAttribute : Attribute
                                          {
                                          }
                                      }

                                      """;
        var source = Usings + ShipmentTypes + toolsAttribute + ShipmentFilterWithIds + ControlRepository +
                     RepositoryWithoutApplyFilter("[Tools.GenerateFilterQuery]\npublic partial class");

        // Act
        var generatedHintNames = GeneratedHintNames(source);

        // Assert
        Assert.Equal([ControlRepositoryHintName], generatedHintNames);
    }

    [Fact]
    public void FilterQuery_WritesApplyFilter_ForARepositoryOnTheConsumersOwnBase()
    {
        // Arrange: a codebase with its own data library has its own repository base, shaped like MagicCSharp's.
        const string source = Usings + ShipmentTypes + ShipmentFilterWithIds + """

                                                                               public abstract class PlatformRepository<TDal, TFilter>
                                                                               {
                                                                                   protected abstract IQueryable<TDal> ApplyFilter(IQueryable<TDal> query, TFilter filter);
                                                                               }

                                                                               [GenerateFilterQuery]
                                                                               public partial class ShipmentsRepository : PlatformRepository<ShipmentDal, ShipmentFilter>;
                                                                               """;

        // Act
        var generatorRunResult = RunClean(source);

        // Assert
        Assert.Contains("query = global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyListFilter<global::Acme.Shipping.Data.ShipmentDal, long>(query, filter.Ids, row => row.Id);",
            generatorRunResult.GeneratedText(RepositoryHintName));
    }

    [Fact]
    public void FilterQuery_Compiles_WhenTheConsumerHasItsOwnFilterHelpersInScope()
    {
        // Arrange: a codebase's own same-named extension methods, imported where the repository lives, would make an
        // extension-method call ambiguous; the generated static calls are not.
        const string source = Usings + """
                                       using Acme.Platform.Utils;

                                       """ + ShipmentTypes + ShipmentFilterWithIds + ShipmentsRepository;
        const string platformHelpers = """
                                       using System;
                                       using System.Collections.Generic;
                                       using System.Linq;
                                       using System.Linq.Expressions;

                                       namespace Acme.Platform.Utils;

                                       public static class QueryFilterExtensions
                                       {
                                           public static IQueryable<T> ApplyListFilter<T, TValue>(this IQueryable<T> query, IEnumerable<TValue>? values, Expression<Func<T, TValue?>> fieldSelector)
                                               where TValue : struct
                                           {
                                               return query;
                                           }
                                       }
                                       """;
        var generatorRunRequest = Request(source) with
        {
            SourceFiles = [GeneratorHarness.Subject(source), new TestSourceFile { Path = "PlatformHelpers.cs", Text = platformHelpers }],
        };

        // Act
        var generatorRunResult = GeneratorHarness.Run(generatorRunRequest);

        // Assert
        Assert.Empty(generatorRunResult.Diagnostics);
        Assert.Contains("global::MagicCSharp.Data.Utils.QueryFilterExtensions.ApplyListFilter", generatorRunResult.GeneratedText(RepositoryHintName));
    }

    private const string ShipmentFilterWithIds = """
                                                 public class ShipmentFilter
                                                 {
                                                     public List<long>? Ids { get; init; }
                                                 }

                                                 """;

    private const string ControlRepositoryHintName = "Acme.Shipping.Data.ShipmentsControlRepository.FilterQuery.g.cs";

    private const string ControlRepository = """

                                             [GenerateFilterQuery]
                                             public partial class ShipmentsControlRepository(RepositoryServices<ShippingContext> repositoryServices)
                                                 : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices);

                                             """;

    // The shipment types with a 'Weight' the entity has and the Dal stores as the given column, or not at all.
    private static string EntityWithWeight(string dalWeightColumn)
    {
        const string template = """
                                namespace Acme.Shipping.Data;

                                public class ShippingContext(DbContextOptions options) : DbContext(options);

                                public record ShipmentEdit
                                {
                                    public required decimal Weight { get; init; }
                                }

                                public record Shipment : ShipmentEdit, IIdEntity
                                {
                                    public required long Id { get; init; }
                                    public required DateTimeOffset Created { get; init; }
                                    public required DateTimeOffset Updated { get; init; }
                                }

                                public class ShipmentDal : BaseIdDal<Shipment, ShipmentEdit>
                                {
                                    DAL_WEIGHT_COLUMN

                                    public override Shipment ToEntity()
                                    {
                                        return new Shipment { Id = Id, Created = Created, Updated = Updated, Weight = 0 };
                                    }

                                    public override void Apply(ShipmentEdit edit)
                                    {
                                    }
                                }


                                """;
        return template.Replace("DAL_WEIGHT_COLUMN", dalWeightColumn);
    }

    // The shipment repository, which leaves ApplyFilter to the generator when it is partial and marked.
    private static string RepositoryWithoutApplyFilter(string classDeclaration)
    {
        return $"""

                {classDeclaration} ShipmentsRepository(RepositoryServices<ShippingContext> repositoryServices)
                    : BaseIdRepository<ShippingContext, ShipmentDal, Shipment, ShipmentFilter, ShipmentEdit>(repositoryServices);
                """;
    }

    // The generated ApplyFilter of the shipment repository over a filter with the one given field.
    private static string GeneratedApplyFilter(string filterField)
    {
        const string filterTemplate = """
                                      public class ShipmentFilter
                                      {
                                          FILTER_FIELD
                                      }
                                      """;
        var source = Usings + ShipmentTypes + filterTemplate.Replace("FILTER_FIELD", filterField) + ShipmentsRepository;
        return RunClean(source).GeneratedText(RepositoryHintName);
    }

    private static GeneratorRunRequest Request(string source)
    {
        return new GeneratorRunRequest
        {
            SourceFiles = [GeneratorHarness.Subject(source)],
            OtherGenerators = [new FilterQueryGenerator()],
            AdditionalReferences = DataReferences,
        };
    }

    // Generates, checks the generated-code rules, and requires the result to compile without a diagnostic.
    private static GeneratorRunResult RunClean(string source)
    {
        var generatorRunResult = GeneratorHarness.Run(Request(source));
        Assert.Empty(generatorRunResult.Diagnostics);
        return generatorRunResult;
    }

    // The files the generator writes, without asking the compilation to compile.
    private static string[] GeneratedHintNames(string source)
    {
        var generatorRunRequest = Request(source);
        var compilation = GeneratorHarness.CreateCompilation(generatorRunRequest.SourceFiles,
            GeneratorHarness.DefaultReferences().AddRange(DataReferences));
        var runResult = GeneratorHarness.CreateDriver(generatorRunRequest, false).RunGenerators(compilation);
        return runResult.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(generatedSource => generatedSource.HintName).Order().ToArray();
    }

    // The generator's diagnostics alone. A refused repository is left without ApplyFilter and does not compile, so
    // this does not ask the compilation to.
    private static string[] Diagnostics(string source)
    {
        var generatorRunRequest = Request(source);
        var compilation = GeneratorHarness.CreateCompilation(generatorRunRequest.SourceFiles,
            GeneratorHarness.DefaultReferences().AddRange(DataReferences));
        var runResult = GeneratorHarness.CreateDriver(generatorRunRequest, false).RunGenerators(compilation);
        return runResult.GetRunResult().Diagnostics.Select(GeneratorHarness.Describe).ToArray();
    }
}
