namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>
///     The base class and registration method generated clients use: the defaults in <c>MagicCSharp.Clients</c>, or
///     the ones a <c>[MagicClientConvention]</c> names.
/// </summary>
internal sealed record ClientConvention
{
    /// <summary>The assemblies declaring <c>[MagicClientConvention]</c>; more than one refuses every client.</summary>
    public required EquatableArray<string> DeclaringAssemblies { get; init; }

    public required string BaseFullyQualifiedName { get; init; }

    /// <summary>The base's accessible constructors that take parameters; each is copied onto the client class.</summary>
    public required EquatableArray<MethodDeclaration> BaseConstructors { get; init; }

    /// <summary>The members of <see cref="ClientBaseMembers" /> the base offers.</summary>
    public required EquatableArray<string> BaseMembers { get; init; }

    public required string RegistrationTypeFullyQualifiedName { get; init; }

    public required string RegistrationMethod { get; init; }

    public required bool HasRegistrationMethod { get; init; }
}
