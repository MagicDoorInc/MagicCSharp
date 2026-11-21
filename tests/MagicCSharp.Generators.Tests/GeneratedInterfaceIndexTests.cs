namespace MagicCSharp.Generators.Tests;

public class GeneratedInterfaceIndexTests
{
    private const string ChargesSource = """
                                         using System.Threading.Tasks;
                                         using MagicCSharp.UseCases;

                                         namespace Acme.Leasing.Charges;

                                         [GenerateInterface]
                                         public partial class PayChargeUseCase
                                         {
                                             public Task Execute(long chargeId)
                                             {
                                                 return Task.CompletedTask;
                                             }
                                         }
                                         """;

    private const string RefundsSource = """
                                         using System.Threading.Tasks;
                                         using MagicCSharp.UseCases;

                                         namespace Acme.Leasing.Refunds;

                                         [GenerateInterface]
                                         public partial class PayChargeUseCase
                                         {
                                             public Task Execute(long refundId, decimal amount)
                                             {
                                                 return Task.CompletedTask;
                                             }
                                         }
                                         """;

    [Fact]
    public void TryFind_FindsNothing_WhenTwoUseCasesShareTheInterfaceNameAndTheReferenceNamesNoNamespace()
    {
        // Arrange
        const string describedSource = """
                                       using Acme.Descriptions;
                                       using Acme.Leasing.Charges;

                                       namespace Acme.Reports;

                                       [DescribeUseCase(typeof(IPayChargeUseCase))]
                                       public static partial class PayChargeDescription
                                       {
                                       }
                                       """;

        // Act
        var description = Describe(describedSource, ChargesSource, RefundsSource);

        // Assert
        Assert.Contains("UseCaseClass = \"not found\"", description);
    }

    [Fact]
    public void TryFind_FindsTheUseCaseOfTheNamedNamespace_WhenTwoUseCasesShareTheInterfaceName()
    {
        // Arrange
        const string describedSource = """
                                       using Acme.Descriptions;

                                       namespace Acme.Reports;

                                       [DescribeUseCase(typeof(Acme.Leasing.Refunds.IPayChargeUseCase))]
                                       public static partial class PayChargeDescription
                                       {
                                       }
                                       """;

        // Act
        var description = Describe(describedSource, ChargesSource, RefundsSource);

        // Assert
        Assert.Contains("UseCaseClass = \"global::Acme.Leasing.Refunds.PayChargeUseCase\"", description);
        Assert.Contains("ExecuteParameters = \"long, decimal\"", description);
    }

    [Fact]
    public void TryFind_FindsNothing_WhenAResolvedInterfaceOfAnotherNamespaceHasTheSameName()
    {
        // Arrange
        const string handWrittenInterfaceSource = """
                                                  namespace Acme.Other;

                                                  public interface IPayChargeUseCase
                                                  {
                                                  }
                                                  """;
        const string describedSource = """
                                       using Acme.Descriptions;

                                       namespace Acme.Reports;

                                       [DescribeUseCase(typeof(Acme.Other.IPayChargeUseCase))]
                                       public static partial class PayChargeDescription
                                       {
                                       }
                                       """;

        // Act
        var description = Describe(describedSource, ChargesSource, handWrittenInterfaceSource);

        // Assert
        Assert.Contains("UseCaseClass = \"not found\"", description);
    }

    private static string Describe(string describedSource, params string[] useCaseSources)
    {
        var useCaseFiles = useCaseSources.Select((useCaseSource, index) => new TestSourceFile { Path = "UseCase" + index + ".cs", Text = useCaseSource });
        var generatorRunResult = GeneratorHarness.Run(new GeneratorRunRequest
        {
            SourceFiles =
            [
                .. useCaseFiles,
                new TestSourceFile { Path = "DescribeUseCaseAttribute.cs", Text = UseCaseDescriptionGenerator.AttributeSource },
                new TestSourceFile { Path = "PayChargeDescription.cs", Text = describedSource },
            ],
            OtherGenerators = [new UseCaseDescriptionGenerator()],
        });
        Assert.Empty(generatorRunResult.Diagnostics);
        return generatorRunResult.GeneratedText("Acme.Reports.PayChargeDescription.Description.g.cs");
    }
}
