using MagicCSharp.Generators.Declarations;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Tests;

/// <summary>
///     A generator built on the declarations library the way a generator in another repository is: it reads
///     <c>[DescribeUseCase(typeof(IX))]</c> or <c>[DescribeUseCase("Namespace.IX")]</c>, where <c>IX</c> is generated
///     by <see cref="GenerateInterfaceGenerator" /> in the same compilation and so does not exist for it, finds the
///     use case through <see cref="GeneratedInterfaceIndex" />, and writes what it found as constants.
/// </summary>
public sealed class UseCaseDescriptionGenerator : IIncrementalGenerator
{
    public const string AttributeSource = """
                                          namespace Acme.Descriptions;

                                          [System.AttributeUsage(System.AttributeTargets.Class)]
                                          public sealed class DescribeUseCaseAttribute : System.Attribute
                                          {
                                              public DescribeUseCaseAttribute(System.Type useCaseInterface)
                                              {
                                              }

                                              public DescribeUseCaseAttribute(string useCaseInterfaceName)
                                              {
                                              }
                                          }
                                          """;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var describedUseCases = context.SyntaxProvider.ForAttributeWithMetadataName("Acme.Descriptions.DescribeUseCaseAttribute",
            (_, _) => true,
            (attributeContext, cancellationToken) => new DescribedUseCase
            {
                DescriptionClass = DeclarationReader.ReadType((INamedTypeSymbol)attributeContext.TargetSymbol, cancellationToken),
                NamedInterface = DeclarationReader.ReadAttribute(attributeContext.Attributes[0]).ConstructorArguments[0],
            });
        var index = UseCasePipelines.GeneratedInterfaceIndex(context);
        context.RegisterSourceOutput(describedUseCases.Combine(index), WriteDescription);
    }

    private static void WriteDescription(SourceProductionContext sourceProductionContext, (DescribedUseCase Described, GeneratedInterfaceIndex Index) describedAndIndex)
    {
        var namedInterface = describedAndIndex.Described.NamedInterface;
        var isFound = namedInterface.Kind == AttributeArgumentKind.Type
            ? describedAndIndex.Index.TryFind(namedInterface.Type!, out var declaration)
            : describedAndIndex.Index.TryFind(namedInterface.Value!, out declaration);

        var descriptionClass = describedAndIndex.Described.DescriptionClass;
        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();
        sourceWriter.WriteLine("namespace " + descriptionClass.Namespace + ";");
        sourceWriter.WriteLine();
        sourceWriter.WriteLine("partial class " + descriptionClass.Name);
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine("public const string UseCaseClass = \"" + (isFound ? declaration!.Class.FullyQualifiedName : "not found") + "\";");
        var executeParameters = isFound
            ? string.Join(" | ", declaration!.ExecuteMethods.Select(method => string.Join(", ", method.Parameters.Select(parameter => parameter.Type.FullyQualifiedName))))
            : "";
        sourceWriter.WriteLine("public const string ExecuteParameters = \"" + executeParameters + "\";");
        sourceWriter.CloseBlock();
        sourceProductionContext.AddSource(HintNames.For(descriptionClass, "Description"), sourceWriter.ToString());
    }
}
