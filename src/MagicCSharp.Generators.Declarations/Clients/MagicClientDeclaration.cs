namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>A <c>[MagicClient]</c> interface, as its client class and registration are written from it.</summary>
internal sealed record MagicClientDeclaration
{
    public required TypeDeclaration Interface { get; init; }

    /// <summary>The client class's name: the interface's without its leading <c>I</c>.</summary>
    public required string ClientName { get; init; }

    public required string? UrlSetting { get; init; }

    public required string? DefaultUrl { get; init; }

    /// <summary>The methods to generate: route-attributed, and not already in the hand-written part of the class.</summary>
    public required EquatableArray<ClientMethod> Methods { get; init; }

    /// <summary>The class has a hand-written part, which carries its accessibility.</summary>
    public required bool HasHandWrittenPart { get; init; }

    /// <summary>The hand-written part declares a constructor, so none is copied from the base.</summary>
    public required bool HasHandWrittenConstructor { get; init; }

    public required ClientConvention Convention { get; init; }
}
