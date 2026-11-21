using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Reads a method symbol, with its parameters, into the <see cref="MethodDeclaration" /> generators write from.</summary>
public static class MethodDeclarationReader
{
    public static MethodDeclaration Read(IMethodSymbol method, CancellationToken cancellationToken)
    {
        return new MethodDeclaration
        {
            Name = method.Name,
            ReturnType = TypeReferenceReader.Read(method.ReturnType),
            Parameters = new EquatableArray<ParameterDeclaration>(method.Parameters.Select(ReadParameter)),
            TypeParameters = new EquatableArray<string>(method.TypeParameters.Select(typeParameter => typeParameter.Name)),
            Accessibility = method.DeclaredAccessibility,
            IsStatic = method.IsStatic,
            IsOverride = method.IsOverride,
            IsAbstract = method.IsAbstract,
            Documentation = DocumentationCommentReader.Read(method, cancellationToken),
            Attributes = AttributeDeclarationReader.ReadAll(method.GetAttributes()),
            Location = SourceLocation.From(method.Locations.FirstOrDefault()),
        };
    }

    private static ParameterDeclaration ReadParameter(IParameterSymbol parameter)
    {
        return new ParameterDeclaration
        {
            Name = parameter.Name,
            Type = TypeReferenceReader.Read(parameter.Type),
            RefKind = parameter.RefKind,
            IsParams = parameter.IsParams,
            DefaultValueExpression = parameter.HasExplicitDefaultValue ? DefaultValueReader.ReadExpression(parameter.Type, parameter.ExplicitDefaultValue) : null,
            Attributes = AttributeDeclarationReader.ReadAll(parameter.GetAttributes()),
        };
    }
}
