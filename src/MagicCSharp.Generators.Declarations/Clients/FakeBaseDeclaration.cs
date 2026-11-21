namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>A <c>[GenerateFakeBase]</c> class and the interface members it stubs.</summary>
internal sealed record FakeBaseDeclaration
{
    public required TypeDeclaration Class { get; init; }

    public required string InterfaceFullyQualifiedName { get; init; }

    public required EquatableArray<FakeMember> Members { get; init; }
}
