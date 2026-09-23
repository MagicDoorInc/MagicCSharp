namespace MagicCSharp.Analyzers.Tests;

public class DependencyNameAnalyzerTests
{
    [Fact]
    public async Task Shortened_and_suffixed_dependency_names_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new DependencyNameAnalyzer(), """
                                                                                      namespace Subject;

                                                                                      public interface IUpdateMaintenanceRequestCategoryUseCase
                                                                                      {
                                                                                      }

                                                                                      public interface ILeasesRepository
                                                                                      {
                                                                                      }

                                                                                      public class LeaseService(
                                                                                          IUpdateMaintenanceRequestCategoryUseCase updateCategory,
                                                                                          IUpdateMaintenanceRequestCategoryUseCase updateMaintenanceRequestCategoryUseCase,
                                                                                          ILeasesRepository repository)
                                                                                      {
                                                                                      }
                                                                                      """);

        Assert.Equal([
            "MCS0007 Subject.cs:12 Dependency of type 'IUpdateMaintenanceRequestCategoryUseCase' is named 'updateCategory'; name it 'updateMaintenanceRequestCategory'",
            "MCS0007 Subject.cs:13 Dependency of type 'IUpdateMaintenanceRequestCategoryUseCase' is named 'updateMaintenanceRequestCategoryUseCase'; name it 'updateMaintenanceRequestCategory'",
            "MCS0007 Subject.cs:14 Dependency of type 'ILeasesRepository' is named 'repository'; name it 'leasesRepository'",
        ], diagnostics);
    }

    [Fact]
    public async Task Type_derived_third_party_and_generic_dependencies_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new DependencyNameAnalyzer(), """
                                                                                      namespace Subject;

                                                                                      public interface IGetLeaseUseCase
                                                                                      {
                                                                                      }

                                                                                      public class LeaseService(
                                                                                          IGetLeaseUseCase getLease,
                                                                                          ThirdParty.IExternalService service,
                                                                                          System.Collections.Generic.IEnumerable<IGetLeaseUseCase> handlers)
                                                                                      {
                                                                                          public void Update(IGetLeaseUseCase useCase)
                                                                                          {
                                                                                          }
                                                                                      }
                                                                                      """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Shortened_FromServices_dependency_names_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new DependencyNameAnalyzer(), """
                                                                                      using Microsoft.AspNetCore.Mvc;

                                                                                      namespace Subject;

                                                                                      public interface IGetOwnerPackagesPropertyManagerExternalUseCase
                                                                                      {
                                                                                      }

                                                                                      public class ReportsController : ControllerBase
                                                                                      {
                                                                                          public void GetOwnerPackages(
                                                                                              long companyId,
                                                                                              [FromServices] IGetOwnerPackagesPropertyManagerExternalUseCase getOwnerPackages,
                                                                                              [FromServices] IGetOwnerPackagesPropertyManagerExternalUseCase useCase)
                                                                                          {
                                                                                          }

                                                                                          public void GetOwnerPackagesAgain(
                                                                                              [FromServices] IGetOwnerPackagesPropertyManagerExternalUseCase getOwnerPackagesPropertyManagerExternal)
                                                                                          {
                                                                                          }
                                                                                      }
                                                                                      """);

        Assert.Equal([
            "MCS0007 Subject.cs:13 Dependency of type 'IGetOwnerPackagesPropertyManagerExternalUseCase' is named 'getOwnerPackages'; name it 'getOwnerPackagesPropertyManagerExternal'",
            "MCS0007 Subject.cs:14 Dependency of type 'IGetOwnerPackagesPropertyManagerExternalUseCase' is named 'useCase'; name it 'getOwnerPackagesPropertyManagerExternal'",
        ], diagnostics);
    }

    [Fact]
    public async Task Sibling_and_MagicCSharp_assemblies_are_first_party_but_a_lookalike_prefix_is_not()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new DependencyNameAnalyzer(), """
                                                                                      namespace Subject;

                                                                                      public class LeaseService(
                                                                                          Acme.Libraries.Shared.ILeaseArchiveService archive,
                                                                                          MagicCSharp.Events.Events.IEventDispatcher dispatcher,
                                                                                          AcmeTools.IAuditService audit)
                                                                                      {
                                                                                      }
                                                                                      """);

        Assert.Equal([
            "MCS0007 Subject.cs:4 Dependency of type 'ILeaseArchiveService' is named 'archive'; name it 'leaseArchiveService'",
            "MCS0007 Subject.cs:5 Dependency of type 'IEventDispatcher' is named 'dispatcher'; name it 'eventDispatcher'",
        ], diagnostics);
    }
}
