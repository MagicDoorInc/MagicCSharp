namespace MagicCSharp.Generators.Declarations.AssemblyLoading;

/// <summary>The loader one project gets: its namespace and the referenced projects it loads, by name.</summary>
public sealed record AssemblyLoaderDeclaration
{
    /// <summary>The project's root namespace, or its assembly name when it has none; empty for the global namespace.</summary>
    public required string Namespace { get; init; }

    public required EquatableArray<LoadedAssembly> Assemblies { get; init; }
}
