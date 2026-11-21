namespace MagicCSharp.Generators.Declarations.ContextFactories;

/// <summary>A context marked <c>[MigrationsDatabase]</c>, as much as its factory needs.</summary>
public sealed record MigrationsDatabaseContext
{
    public required string Name { get; init; }

    /// <summary>Empty for the global namespace.</summary>
    public required string Namespace { get; init; }

    /// <summary><c>global::</c>-qualified.</summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary><c>public</c> or <c>internal</c>, as the context is.</summary>
    public required string Accessibility { get; init; }

    /// <summary>The attribute's name argument; null when it is not a string.</summary>
    public required string? DatabaseName { get; init; }

    /// <summary><c>global::</c>-qualified names of every base class and interface, for the base's constraint.</summary>
    public required EquatableArray<string> BaseTypesAndInterfaces { get; init; }

    public required bool IsNested { get; init; }

    public required bool IsGeneric { get; init; }

    public required bool IsAbstract { get; init; }

    public required SourceLocation? Location { get; init; }
}
