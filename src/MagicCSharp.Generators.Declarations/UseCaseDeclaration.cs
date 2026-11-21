namespace MagicCSharp.Generators.Declarations;

/// <summary>A <c>[GenerateInterface]</c> use case class and the interface it gets.</summary>
public sealed record UseCaseDeclaration
{
    public required TypeDeclaration Class { get; init; }

    /// <summary><c>I</c> and the class name: <c>ISignLeaseUseCase</c>.</summary>
    public required string InterfaceName { get; init; }

    /// <summary><c>global::</c>-qualified, in the class's namespace.</summary>
    public required string InterfaceFullyQualifiedName { get; init; }

    /// <summary>The use case marker interface the generated interface extends.</summary>
    public required TypeReference MarkerInterface { get; init; }

    /// <summary>The class's public instance <c>Execute</c> methods, in declaration order: the interface's members.</summary>
    public required EquatableArray<MethodDeclaration> ExecuteMethods { get; init; }
}
