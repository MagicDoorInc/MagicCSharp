using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Reads a <c>[GenerateInterface]</c> class into the interface it gets, or the reasons it gets none.</summary>
public static class UseCaseReader
{
    // Stands in until the pipeline joins the class with the marker interface, which comes from the whole
    // compilation rather than the class.
    private static readonly TypeReference PendingMarkerInterface = TypeReferenceReader.Unresolved("");

    /// <param name="useCaseClass">The class marked <c>[GenerateInterface]</c>.</param>
    /// <param name="markerInterface">The use case marker interface; null when the compilation has none, which refuses the class.</param>
    /// <param name="cancellationToken">Stops the read when the compilation it belongs to is replaced.</param>
    public static UseCaseReadResult Read(INamedTypeSymbol useCaseClass, INamedTypeSymbol? markerInterface, CancellationToken cancellationToken)
    {
        var classReading = ReadClass(useCaseClass, cancellationToken);
        var markerInterfaceLookup = new MarkerInterfaceLookup
        {
            MetadataName = markerInterface == null ? UseCaseMarkerInterfaceSetting.DefaultMetadataName : markerInterface.ToDisplayString(),
            Interface = markerInterface == null || markerInterface.TypeKind != TypeKind.Interface ? null : TypeReferenceReader.Read(markerInterface),
        };
        return WithMarkerInterface(classReading, markerInterfaceLookup);
    }

    internal static UseCaseReadResult ReadClass(INamedTypeSymbol useCaseClass, CancellationToken cancellationToken)
    {
        var interfaceName = "I" + useCaseClass.Name;
        if (ImplementsOwnInterface(useCaseClass, interfaceName))
        {
            return Skipped();
        }

        var classDeclaration = TypeDeclarationReader.Read(useCaseClass, cancellationToken);
        var shapeProblems = ShapeProblems(classDeclaration);
        if (shapeProblems.Count > 0)
        {
            return Refused(shapeProblems);
        }

        var interfaceMethods = InterfaceMethods(useCaseClass);
        if (!interfaceMethods.Any(method => method.Name == UseCaseDeclaration.ExecuteMethodName))
        {
            return Refused([Problem(GeneratorDiagnostics.NoPublicExecute, classDeclaration.Location, classDeclaration.Name)]);
        }

        return new UseCaseReadResult
        {
            Declaration = BuildDeclaration(classDeclaration, interfaceName, interfaceMethods, cancellationToken),
            IsSkipped = false,
            Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
        };
    }

    private static UseCaseDeclaration BuildDeclaration(TypeDeclaration classDeclaration, string interfaceName, IMethodSymbol[] interfaceMethods, CancellationToken cancellationToken)
    {
        var namespacePrefix = classDeclaration.Namespace == "" ? "" : classDeclaration.Namespace + ".";
        return new UseCaseDeclaration
        {
            Class = classDeclaration,
            InterfaceName = interfaceName,
            InterfaceFullyQualifiedName = GlobalAlias.Prefix + namespacePrefix + interfaceName,
            MarkerInterface = PendingMarkerInterface,
            Methods = new EquatableArray<MethodDeclaration>(interfaceMethods.Select(method => MethodDeclarationReader.Read(method, cancellationToken))),
        };
    }

    // Every public instance method the class declares in any of its parts, except what belongs to another contract:
    // an override belongs to the base class, an implementation of another interface's member (IDisposable.Dispose) to
    // that interface, and a member the compiler writes (a record's Equals and Deconstruct) to the language.
    private static IMethodSymbol[] InterfaceMethods(INamedTypeSymbol useCaseClass)
    {
        var otherInterfaceImplementations = OtherInterfaceImplementations(useCaseClass);
        return useCaseClass.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => IsOwnPublicInstanceMethod(method) && !otherInterfaceImplementations.Contains(method))
            .ToArray();
    }

    private static bool IsOwnPublicInstanceMethod(IMethodSymbol method)
    {
        return method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public && !method.IsStatic && !method.IsOverride &&
               !method.IsImplicitlyDeclared;
    }

    // The interfaces the class lists that the compiler resolves; its own generated interface is not yet among them.
    private static HashSet<IMethodSymbol> OtherInterfaceImplementations(INamedTypeSymbol useCaseClass)
    {
        var implementations = useCaseClass.AllInterfaces.SelectMany(implementedInterface => implementedInterface.GetMembers())
            .Select(useCaseClass.FindImplementationForInterfaceMember)
            .OfType<IMethodSymbol>();
        return new HashSet<IMethodSymbol>(implementations, SymbolEqualityComparer.Default);
    }

    internal static UseCaseReadResult WithMarkerInterface(UseCaseReadResult classReading, MarkerInterfaceLookup markerInterfaceLookup)
    {
        if (classReading.Declaration == null)
        {
            return classReading;
        }

        if (markerInterfaceLookup.Interface == null)
        {
            var useCaseClass = classReading.Declaration.Class;
            return Refused([Problem(GeneratorDiagnostics.MarkerInterfaceNotFound, useCaseClass.Location, useCaseClass.Name, markerInterfaceLookup.MetadataName)]);
        }

        return classReading with
        {
            Declaration = classReading.Declaration with { MarkerInterface = markerInterfaceLookup.Interface },
        };
    }

    // Only a resolved interface counts: a class that lists IX before anything declares it still gets IX.
    private static bool ImplementsOwnInterface(INamedTypeSymbol useCaseClass, string interfaceName)
    {
        return useCaseClass.Interfaces.Any(listedInterface => listedInterface.Name == interfaceName && listedInterface.TypeKind == TypeKind.Interface);
    }

    private static List<DiagnosticInfo> ShapeProblems(TypeDeclaration classDeclaration)
    {
        var shapeProblems = new List<DiagnosticInfo>();
        if (classDeclaration.IsNested)
        {
            shapeProblems.Add(Problem(GeneratorDiagnostics.NestedClass, classDeclaration.Location, classDeclaration.Name));
        }

        if (classDeclaration.TypeParameters.Count > 0)
        {
            shapeProblems.Add(Problem(GeneratorDiagnostics.GenericClass, classDeclaration.Location, classDeclaration.Name));
        }

        if (classDeclaration.IsStatic)
        {
            shapeProblems.Add(Problem(GeneratorDiagnostics.StaticClass, classDeclaration.Location, classDeclaration.Name));
        }

        if (!classDeclaration.IsPartial)
        {
            shapeProblems.Add(Problem(GeneratorDiagnostics.NotPartial, classDeclaration.Location, classDeclaration.Name));
        }

        return shapeProblems;
    }

    private static DiagnosticInfo Problem(DiagnosticDescriptor descriptor, SourceLocation? location, params string[] messageArguments)
    {
        return new DiagnosticInfo
        {
            Descriptor = descriptor,
            Location = location,
            MessageArguments = new EquatableArray<string>(messageArguments),
        };
    }

    private static UseCaseReadResult Skipped()
    {
        return new UseCaseReadResult
        {
            Declaration = null,
            IsSkipped = true,
            Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
        };
    }

    private static UseCaseReadResult Refused(IEnumerable<DiagnosticInfo> problems)
    {
        return new UseCaseReadResult
        {
            Declaration = null,
            IsSkipped = false,
            Diagnostics = new EquatableArray<DiagnosticInfo>(problems),
        };
    }
}
