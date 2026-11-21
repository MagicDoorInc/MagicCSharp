namespace MagicCSharp.Generators.Declarations;

/// <summary>The configured marker interface's name and, when the compilation has it as an interface, the interface.</summary>
internal sealed record MarkerInterfaceLookup
{
    public required string MetadataName { get; init; }

    public required TypeReference? Interface { get; init; }
}
