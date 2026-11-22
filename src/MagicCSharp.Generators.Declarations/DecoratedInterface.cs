namespace MagicCSharp.Generators.Declarations;

/// <summary>The use case interface a <c>[LogExecuting]</c> decorator implements, and every method it forwards.</summary>
internal sealed record DecoratedInterface
{
    /// <summary><c>global::</c>-qualified.</summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>Its name as a message shows it, <c>Acme.Leasing.IPayChargeUseCase</c>.</summary>
    public required string DisplayName { get; init; }

    public required EquatableArray<MethodDeclaration> Methods { get; init; }
}
