using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Generators.Declarations.AssemblyLoading;

/// <summary>
///     The build properties the assembly loader is written from: whether the project asked for it, the projects it
///     references, and its root namespace.
/// </summary>
public sealed record AssemblyLoaderSettings
{
    /// <summary><c>true</c> in a project that wants its <c>AssemblyLoader</c> generated, usually the host.</summary>
    public const string OptInPropertyName = "MagicCSharpGenerateAssemblyLoader";

    /// <summary>
    ///     The referenced projects' assembly file names, comma-separated (the compiler reads it from an editorconfig, where <c>;</c> starts a comment). The package's targets file fills it from
    ///     the resolved project references, transitive ones included; nobody sets it by hand.
    /// </summary>
    public const string ReferencedProjectsPropertyName = "MagicCSharpReferencedProjectAssemblies";

    private const string RootNamespacePropertyName = "RootNamespace";

    public required bool IsOptedIn { get; init; }

    public required EquatableArray<string> ReferencedProjectAssemblies { get; init; }

    /// <summary>The project's root namespace; empty when the build does not say.</summary>
    public required string RootNamespace { get; init; }

    public static AssemblyLoaderSettings Read(AnalyzerConfigOptionsProvider options)
    {
        return new AssemblyLoaderSettings
        {
            IsOptedIn = string.Equals(BuildProperty(options, OptInPropertyName), "true", StringComparison.OrdinalIgnoreCase),
            ReferencedProjectAssemblies = new EquatableArray<string>(BuildProperty(options, ReferencedProjectsPropertyName)
                .Split(',')
                .Select(assemblyName => assemblyName.Trim())
                .Where(assemblyName => assemblyName.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(assemblyName => assemblyName, StringComparer.Ordinal)),
            RootNamespace = BuildProperty(options, RootNamespacePropertyName).Trim(),
        };
    }

    private static string BuildProperty(AnalyzerConfigOptionsProvider options, string propertyName)
    {
        if (options.GlobalOptions.TryGetValue("build_property." + propertyName, out var value) && value != null)
        {
            return value;
        }

        return "";
    }
}
