using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Incremental pipelines over the <c>[GenerateInterface]</c> classes of a compilation. MagicCSharp's interface
///     generator uses them, and so can any other generator that needs those interfaces before they exist.
/// </summary>
public static class UseCasePipelines
{
    public const string GenerateInterfaceAttributeMetadataName = "MagicCSharp.UseCases.GenerateInterfaceAttribute";

    /// <summary>One result per <c>[GenerateInterface]</c> class: the declaration to generate, a skip, or a refusal.</summary>
    public static IncrementalValuesProvider<UseCaseReadResult> GeneratedInterfaceReadResults(IncrementalGeneratorInitializationContext context)
    {
        var markerInterfaceLookup = context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider)
            .Select((compilationAndOptions, _) => FindMarkerInterface(compilationAndOptions.Left, UseCaseMarkerInterfaceSetting.Read(compilationAndOptions.Right)));

        var classReadings = context.SyntaxProvider.ForAttributeWithMetadataName(GenerateInterfaceAttributeMetadataName,
            (node, _) => node is ClassDeclarationSyntax || node is RecordDeclarationSyntax,
            (attributeContext, cancellationToken) => UseCaseReader.ReadClass((INamedTypeSymbol)attributeContext.TargetSymbol, cancellationToken));

        return classReadings.Combine(markerInterfaceLookup)
            .Select((classAndMarker, _) => UseCaseReader.WithMarkerInterface(classAndMarker.Left, classAndMarker.Right));
    }

    /// <summary>The use cases whose interfaces are generated in this compilation, to look up by interface.</summary>
    public static IncrementalValueProvider<GeneratedInterfaceIndex> GeneratedInterfaceIndex(IncrementalGeneratorInitializationContext context)
    {
        return GeneratedInterfaceReadResults(context)
            .Where(readResult => readResult.Declaration != null)
            .Select((readResult, _) => readResult.Declaration!)
            .Collect()
            .Select((declarations, _) => new GeneratedInterfaceIndex { Declarations = new EquatableArray<UseCaseDeclaration>(declarations) });
    }

    private static MarkerInterfaceLookup FindMarkerInterface(Compilation compilation, string metadataName)
    {
        var markerInterface = compilation.GetTypeByMetadataName(metadataName);
        return new MarkerInterfaceLookup
        {
            MetadataName = metadataName,
            Interface = markerInterface == null || markerInterface.TypeKind != TypeKind.Interface ? null : DeclarationReader.ReadTypeReference(markerInterface),
        };
    }
}
