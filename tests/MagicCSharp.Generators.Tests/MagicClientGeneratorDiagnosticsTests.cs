namespace MagicCSharp.Generators.Tests;

public class MagicClientGeneratorDiagnosticsTests
{
    // Declarations start on line 9 of the subject.
    private const string Header = """
                                  using System.Threading.Tasks;
                                  using MagicCSharp.Clients;
                                  using MagicCSharp.Testing;

                                  namespace Acme.Leasing.Clients;

                                  public record LeaseDto { public required long Id { get; init; } }


                                  """;

    [Fact]
    public void MagicClient_ReportsMCS1501_WhenTwoAssembliesDeclareAClientConvention()
    {
        // Arrange
        var sharedCompilation = GeneratorHarness.CreateCompilation([
                    new TestSourceFile
                    {
                        Path = "Shared.cs",
                        Text = """
                               using MagicCSharp.Clients;

                               [assembly: MagicClientConvention(typeof(object), typeof(object), "AddClient")]
                               """,
                    },
                ],
                GeneratorHarness.DefaultReferences().AddRange(ClientGeneratorRuns.References()))
            .WithAssemblyName("Acme.Shared");

        // Act
        var diagnostics = ClientGeneratorRuns.Refusals("""
                                                        using MagicCSharp.Clients;

                                                        [assembly: MagicClientConvention(typeof(object), typeof(object), "AddClient")]

                                                        namespace Acme.Leasing.Clients;

                                                        [MagicClient("api/leases")]
                                                        public interface ILeasesClient
                                                        {
                                                        }
                                                        """, sharedCompilation.ToMetadataReference());

        // Assert
        Assert.Equal([
            "MCS1501 Subject.cs:8 'ILeasesClient' cannot be generated: both 'Acme.Leasing' and 'Acme.Shared' declare [assembly: MagicClientConvention]; keep one",
        ], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1502_WhenTheConventionBaseLacksAMemberTheClientCalls()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals("""
                                                        using System.Threading.Tasks;
                                                        using MagicCSharp.Clients;
                                                        using Microsoft.Extensions.Configuration;
                                                        using Microsoft.Extensions.DependencyInjection;

                                                        [assembly: MagicClientConvention(typeof(Acme.Leasing.Clients.ServiceClientBase), typeof(Acme.Leasing.Clients.ServiceClientRegistration), "AddServiceClient")]

                                                        namespace Acme.Leasing.Clients;

                                                        public abstract class ServiceClientBase
                                                        {
                                                        }

                                                        public static class ServiceClientRegistration
                                                        {
                                                            public static IServiceCollection AddServiceClient<TInterface, TClient>(this IServiceCollection services, IConfiguration configuration, string? baseUrl)
                                                            {
                                                                return services;
                                                            }
                                                        }

                                                        [MagicClient("api/leases")]
                                                        public interface ILeasesClient
                                                        {
                                                            [ClientGet("count")]
                                                            Task<int> CountLeases();
                                                        }
                                                        """);

        // Assert
        Assert.Equal([
            "MCS1502 Subject.cs:23 'ILeasesClient' cannot be generated: its base 'global::Acme.Leasing.Clients.ServiceClientBase' has no 'SendRequestAsync<TResponse>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)'",
        ], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1503_WhenTheConventionRegistrationMethodIsMissing()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals("""
                                                        using System.Threading.Tasks;
                                                        using MagicCSharp.Clients;

                                                        [assembly: MagicClientConvention(typeof(MagicHttpClientBase), typeof(Acme.Leasing.Clients.ServiceClientRegistration), "AddServiceClient")]

                                                        namespace Acme.Leasing.Clients;

                                                        public static class ServiceClientRegistration
                                                        {
                                                        }

                                                        [MagicClient("api/leases")]
                                                        public interface ILeasesClient
                                                        {
                                                            [ClientGet("count")]
                                                            Task<int> CountLeases();
                                                        }
                                                        """);

        // Assert
        Assert.Equal([
            "MCS1503 Subject.cs:13 'ILeasesClient' cannot be registered: 'global::Acme.Leasing.Clients.ServiceClientRegistration' has no static method 'AddServiceClient<TInterface, TClient>(IServiceCollection services, IConfiguration configuration, string? baseUrl)'",
        ], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1504_WhenARouteValueMatchesNoParameter()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [MagicClient("api/leases")]
                                                                 public interface ILeasesClient
                                                                 {
                                                                     [ClientGet("{leaseId}")]
                                                                     Task<LeaseDto> GetLease(long id);
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal([
            "MCS1504 Subject.cs:13 'ILeasesClient.GetLease': the route 'api/leases/{leaseId}' names '{leaseId}', which is not a parameter of the method",
        ], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1505_WhenARouteValueIsNotAStringIntegerGuidOrEnum()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [MagicClient("api/leases")]
                                                                 public interface ILeasesClient
                                                                 {
                                                                     [ClientGet("started-after/{from}")]
                                                                     Task<int> CountLeases(System.DateTimeOffset from);
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal([
            "MCS1505 Subject.cs:13 'ILeasesClient.CountLeases': route value 'from' is a 'System.DateTimeOffset'; a route value must be a string, an integer, a Guid or an enum",
        ], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1506_WhenAWriteHasMoreThanOneObjectParameter()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [MagicClient("api/leases")]
                                                                 public interface ILeasesClient
                                                                 {
                                                                     [ClientPost]
                                                                     Task CreateLease(LeaseDto lease, LeaseDto renewal);
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal([
            "MCS1506 Subject.cs:13 'ILeasesClient.CreateLease' has more than one object parameter outside the route (lease, renewal); a request has one body, so group them in one request type or write the method by hand",
        ], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1507_WhenARouteMethodDoesNotReturnATask()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [MagicClient("api/leases")]
                                                                 public interface ILeasesClient
                                                                 {
                                                                     [ClientGet("count")]
                                                                     ValueTask<int> CountLeases();
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal([
            "MCS1507 Subject.cs:13 'ILeasesClient.CountLeases' returns 'System.Threading.Tasks.ValueTask<int>'; a method with a client route attribute returns Task or Task<T>, or is written by hand without the attribute",
        ], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1508_WhenTheInterfaceIsGeneric()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [MagicClient("api/leases")]
                                                                 public interface ILeasesClient<TLease>
                                                                 {
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal(["MCS1508 Subject.cs:10 'ILeasesClient' is generic; write its client by hand"], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1509_WhenTheInterfaceIsNested()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 public class Leasing
                                                                 {
                                                                     [MagicClient("api/leases")]
                                                                     public interface ILeasesClient
                                                                     {
                                                                     }
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal(["MCS1509 Subject.cs:12 'ILeasesClient' is nested in another type; move it to the top level of its file"], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1510_WhenARouteMethodIsGeneric()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [MagicClient("api/leases")]
                                                                 public interface ILeasesClient
                                                                 {
                                                                     [ClientGet("latest")]
                                                                     Task<TLease> GetLatest<TLease>();
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal(["MCS1510 Subject.cs:13 'ILeasesClient.GetLatest' is generic; remove its client route attribute and write it by hand"], diagnostics);
    }

    [Fact]
    public void MagicClient_ReportsMCS1511_WhenTheInterfaceNameDoesNotStartWithI()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [MagicClient("api/leases")]
                                                                 public interface LeasesApi
                                                                 {
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal([
            "MCS1511 Subject.cs:10 'LeasesApi' does not start with 'I' and a capital letter, so its client class has no name; rename it 'ILeasesApi'",
        ], diagnostics);
    }

    [Fact]
    public void FakeBase_ReportsMCS1512_WhenTheClassIsNotPartial()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [GenerateFakeBase(typeof(ILeaseLookup))]
                                                                 public abstract class FakeLeaseLookupBase
                                                                 {
                                                                 }

                                                                 public interface ILeaseLookup
                                                                 {
                                                                     Task<LeaseDto?> FindLease(long leaseId);
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal([
            "MCS1512 Subject.cs:10 'FakeLeaseLookupBase' has [GenerateFakeBase] but is not partial; add 'partial' so the generated members can be attached to it",
        ], diagnostics);
    }

    [Fact]
    public void FakeBase_ReportsMCS1513_WhenTheClassIsNotAbstract()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [GenerateFakeBase(typeof(ILeaseLookup))]
                                                                 public partial class FakeLeaseLookupBase
                                                                 {
                                                                 }

                                                                 public interface ILeaseLookup
                                                                 {
                                                                     Task<LeaseDto?> FindLease(long leaseId);
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal([
            "MCS1513 Subject.cs:10 'FakeLeaseLookupBase' has [GenerateFakeBase] but is not abstract; a fake base is only inherited, by the fake that overrides what its tests use",
        ], diagnostics);
    }

    [Fact]
    public void FakeBase_ReportsMCS1514_WhenItNamesATypeThatIsNotAnInterface()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [GenerateFakeBase(typeof(LeaseDto))]
                                                                 public abstract partial class FakeLeaseBase
                                                                 {
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal(["MCS1514 Subject.cs:10 'FakeLeaseBase': [GenerateFakeBase] names 'Acme.Leasing.Clients.LeaseDto', which is not an interface"], diagnostics);
    }

    [Fact]
    public void FakeBase_ReportsMCS1515_WhenTheInterfaceHasAGenericMethod()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 [GenerateFakeBase(typeof(ILeaseLookup))]
                                                                 public abstract partial class FakeLeaseLookupBase
                                                                 {
                                                                 }

                                                                 public interface ILeaseLookup
                                                                 {
                                                                     Task<TLease?> FindLease<TLease>(long leaseId);
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal([
            "MCS1515 Subject.cs:10 'FakeLeaseLookupBase': 'ILeaseLookup.FindLease' is generic, which a fake base does not stub; implement the interface in the fake by hand",
        ], diagnostics);
    }

    [Fact]
    public void FakeBase_ReportsMCS1516_WhenTheClassIsNested()
    {
        // Act
        var diagnostics = ClientGeneratorRuns.Refusals(Header + """
                                                                 public class Fakes
                                                                 {
                                                                     [GenerateFakeBase(typeof(ILeaseLookup))]
                                                                     public abstract partial class FakeLeaseLookupBase
                                                                     {
                                                                     }
                                                                 }

                                                                 public interface ILeaseLookup
                                                                 {
                                                                     Task<LeaseDto?> FindLease(long leaseId);
                                                                 }
                                                                 """);

        // Assert
        Assert.Equal(["MCS1516 Subject.cs:12 'FakeLeaseLookupBase' is nested in another type; move it to the top level of its file"], diagnostics);
    }
}
