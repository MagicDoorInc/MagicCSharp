using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.AssemblyLoading;

/// <summary>
///     Reads which referenced projects a project's loader loads. Only the projects the build lists are loaded: a
///     package can be a compile-time reference with nothing at run time to load.
/// </summary>
public static class AssemblyLoaderReader
{
    public const string LoaderClassName = "AssemblyLoader";

    public static AssemblyLoaderReadResult Read(Compilation compilation, AssemblyLoaderSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.IsOptedIn)
        {
            return AssemblyLoaderReadResult.NotOptedIn;
        }

        var loaderNamespace = settings.RootNamespace.Length > 0 ? settings.RootNamespace : compilation.AssemblyName ?? "";
        var loaderMetadataName = loaderNamespace.Length > 0 ? loaderNamespace + "." + LoaderClassName : LoaderClassName;
        var handWrittenLoaderLocation = compilation.GetTypeByMetadataName(loaderMetadataName)?.Locations.FirstOrDefault(location => location.IsInSource);
        if (handWrittenLoaderLocation != null)
        {
            return new AssemblyLoaderReadResult
            {
                Declaration = null,
                Diagnostics = new EquatableArray<DiagnosticInfo>([
                    new DiagnosticInfo
                    {
                        Descriptor = AssemblyLoaderDiagnostics.AssemblyLoaderAlreadyDeclared,
                        Location = SourceLocation.From(handWrittenLoaderLocation),
                        MessageArguments = new EquatableArray<string>([loaderMetadataName]),
                    },
                ]),
            };
        }

        return new AssemblyLoaderReadResult
        {
            Declaration = new AssemblyLoaderDeclaration
            {
                Namespace = loaderNamespace,
                Assemblies = new EquatableArray<LoadedAssembly>(ReferencedProjects(compilation, settings, cancellationToken)),
            },
            Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
        };
    }

    private static IEnumerable<LoadedAssembly> ReferencedProjects(Compilation compilation, AssemblyLoaderSettings settings, CancellationToken cancellationToken)
    {
        var projectAssemblyNames = new HashSet<string>(settings.ReferencedProjectAssemblies, StringComparer.OrdinalIgnoreCase);
        var loadedAssemblies = new Dictionary<string, LoadedAssembly>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in compilation.References)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assembly = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
            if (assembly == null || loadedAssemblies.ContainsKey(assembly.Identity.Name) || !IsListed(assembly, reference, projectAssemblyNames))
            {
                continue;
            }

            loadedAssemblies[assembly.Identity.Name] = new LoadedAssembly
            {
                AssemblyName = assembly.Identity.Name,
                PublicTypeOperand = PublicTypeOperand(assembly),
            };
        }

        return loadedAssemblies.Values.OrderBy(loadedAssembly => loadedAssembly.AssemblyName, StringComparer.Ordinal);
    }

    // The build lists file names; an assembly is almost always named after its file, but either one counts.
    private static bool IsListed(IAssemblySymbol assembly, MetadataReference reference, HashSet<string> projectAssemblyNames)
    {
        var filePath = (reference as PortableExecutableReference)?.FilePath;
        return projectAssemblyNames.Contains(assembly.Identity.Name) ||
               (filePath != null && projectAssemblyNames.Contains(Path.GetFileNameWithoutExtension(filePath)));
    }

    /// <summary>The first public top-level type by full name, non-generic ones first; null when there is none.</summary>
    private static string? PublicTypeOperand(IAssemblySymbol assembly)
    {
        var publicType = PublicTopLevelTypes(assembly.GlobalNamespace)
            .OrderBy(type => type.Arity > 0)
            .ThenBy(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .FirstOrDefault();
        if (publicType == null)
        {
            return null;
        }

        var unboundName = publicType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGenericsOptions(SymbolDisplayGenericsOptions.None));
        return publicType.Arity == 0 ? unboundName : unboundName + "<" + new string(',', publicType.Arity - 1) + ">";
    }

    private static IEnumerable<INamedTypeSymbol> PublicTopLevelTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            if (type.DeclaredAccessibility == Accessibility.Public && type.CanBeReferencedByName)
            {
                yield return type;
            }
        }

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in PublicTopLevelTypes(childNamespace))
            {
                yield return type;
            }
        }
    }
}
