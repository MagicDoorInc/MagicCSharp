namespace MagicCSharp.Generators.Declarations;

/// <summary>An interface a <c>[LogExecuting]</c> class implements, as its decorator would implement it.</summary>
public sealed record ImplementedInterfaceReading
{
    public required TypeReference Interface { get; init; }

    /// <summary>Its metadata name as messages and the marker check use it, <c>Acme.Leasing.IPayChargeUseCase</c>.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The metadata names of every interface it extends, to tell a use case interface from any other.</summary>
    public required EquatableArray<string> ExtendedInterfaceMetadataNames { get; init; }

    /// <summary>
    ///     Its methods and those of the interfaces it extends, with the interface's signatures; each parameter carries
    ///     the attributes of the class's implementation (<c>[NotLogged]</c>), and each method the implementation's location.
    /// </summary>
    public required EquatableArray<MethodDeclaration> Methods { get; init; }
}
