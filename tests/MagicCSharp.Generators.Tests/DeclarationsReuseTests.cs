using MagicCSharp.Generators.Declarations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MagicCSharp.Generators.Tests;

public class DeclarationsReuseTests
{
    private const string PayChargeSource = """
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

                                               public Task Execute(long chargeId, decimal amount)
                                               {
                                                   return Task.CompletedTask;
                                               }
                                           }
                                           """;

    [Fact]
    public void DownstreamGenerator_FindsTheDeclaration_OfAnInterfaceGeneratedInTheSameCompilation()
    {
        // Arrange
        const string descriptionSource = """
                                         using Acme.Descriptions;

                                         namespace Acme.Leasing.Charges;

                                         [DescribeUseCase(typeof(IPayChargeUseCase))]
                                         public static partial class PayChargeDescription
                                         {
                                         }
                                         """;

        // Act
        var generatorRunResult = RunWithDescriptionGenerator(descriptionSource);

        // Assert
        AssertDescribesPayCharge(generatorRunResult);
    }

    [Fact]
    public void GeneratedInterfaceIndex_FindsADeclaration_ByInterfaceFullName()
    {
        // Arrange
        const string descriptionSource = """
                                         using Acme.Descriptions;

                                         namespace Acme.Leasing.Charges;

                                         [DescribeUseCase("Acme.Leasing.Charges.IPayChargeUseCase")]
                                         public static partial class PayChargeDescription
                                         {
                                         }
                                         """;

        // Act
        var generatorRunResult = RunWithDescriptionGenerator(descriptionSource);

        // Assert
        AssertDescribesPayCharge(generatorRunResult);
    }

    [Fact]
    public void ReadDocumentation_ReadsTheDocComment_OfAMethodInAReferencedAssembly()
    {
        // Arrange
        const string librarySource = """
                                     namespace Acme.Payments;

                                     public class RefundChargeUseCase
                                     {
                                         /// <summary>Refunds a paid charge.</summary>
                                         /// <param name="chargeId">The charge to refund.</param>
                                         public void Execute(long chargeId)
                                         {
                                         }
                                     }
                                     """;
        var libraryReference = CompileWithDocumentation("Acme.Payments", librarySource);
        var compilation = GeneratorHarness.CreateCompilation([GeneratorHarness.Subject("namespace Acme.Leasing;")],
            GeneratorHarness.DefaultReferences().Add(libraryReference));
        var executeMethod = compilation.GetTypeByMetadataName("Acme.Payments.RefundChargeUseCase")!.GetMembers("Execute").OfType<IMethodSymbol>().Single();

        // Act
        var documentation = DeclarationReader.ReadDocumentation(executeMethod, CancellationToken.None);

        // Assert
        Assert.Equal([" <summary>Refunds a paid charge.</summary>", " <param name=\"chargeId\">The charge to refund.</param>"], documentation.Lines);
    }

    private static GeneratorRunResult RunWithDescriptionGenerator(string descriptionSource)
    {
        var generatorRunResult = GeneratorHarness.Run(new GeneratorRunRequest
        {
            SourceFiles =
            [
                new TestSourceFile { Path = "PayChargeUseCase.cs", Text = PayChargeSource },
                new TestSourceFile { Path = "DescribeUseCaseAttribute.cs", Text = UseCaseDescriptionGenerator.AttributeSource },
                new TestSourceFile { Path = "PayChargeDescription.cs", Text = descriptionSource },
            ],
            OtherGenerators = [new UseCaseDescriptionGenerator()],
        });
        Assert.Empty(generatorRunResult.Diagnostics);
        return generatorRunResult;
    }

    private static void AssertDescribesPayCharge(GeneratorRunResult generatorRunResult)
    {
        var description = generatorRunResult.GeneratedText("Acme.Leasing.Charges.PayChargeDescription.Description.g.cs");
        Assert.Contains("public const string UseCaseClass = \"global::Acme.Leasing.Charges.PayChargeUseCase\";", description);
        Assert.Contains("public const string ExecuteParameters = \"long | long, decimal\";", description);
    }

    private static MetadataReference CompileWithDocumentation(string assemblyName, string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose);
        var compilation = CSharpCompilation.Create(assemblyName, [CSharpSyntaxTree.ParseText(source, parseOptions, assemblyName + ".cs")],
            GeneratorHarness.DefaultReferences(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var peStream = new MemoryStream();
        using var documentationStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream, xmlDocumentationStream: documentationStream);
        Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));
        return MetadataReference.CreateFromImage(peStream.ToArray(), documentation: new XmlFileDocumentationProvider(documentationStream.ToArray()));
    }
}
