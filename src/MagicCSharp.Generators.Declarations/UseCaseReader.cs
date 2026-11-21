using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>Reads a <c>[GenerateInterface]</c> class into the interface it gets, or the reasons it gets none.</summary>
public static class UseCaseReader
{
    private const string ExecuteMethodName = "Execute";

    // Stands in until the pipeline joins the class with the marker interface, which comes from the whole
    // compilation rather than the class.
    private static readonly TypeReference PendingMarkerInterface = new TypeReference
    {
        Name = "",
        Namespace = "",
        FullyQualifiedName = "",
        Kind = TypeReferenceKind.Unresolved,
        IsNullable = false,
        IsValueType = false,
        IsUnresolved = true,
        TypeArguments = EquatableArray<TypeReference>.Empty,
        ElementType = null,
    };

    /// <param name="useCaseClass">The class marked <c>[GenerateInterface]</c>.</param>
    /// <param name="markerInterface">The use case marker interface; null when the compilation has none, which refuses the class.</param>
    /// <param name="cancellationToken">Stops the read when the compilation it belongs to is replaced.</param>
    public static UseCaseReadResult Read(INamedTypeSymbol useCaseClass, INamedTypeSymbol? markerInterface, CancellationToken cancellationToken)
    {
        var classReading = ReadClass(useCaseClass, cancellationToken);
        var markerInterfaceLookup = new MarkerInterfaceLookup
        {
            MetadataName = markerInterface == null ? UseCaseMarkerInterfaceSetting.DefaultMetadataName : markerInterface.ToDisplayString(),
            Interface = markerInterface == null || markerInterface.TypeKind != TypeKind.Interface ? null : DeclarationReader.ReadTypeReference(markerInterface),
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

        var classDeclaration = DeclarationReader.ReadType(useCaseClass, cancellationToken);
        var shapeProblems = ShapeProblems(classDeclaration);
        if (shapeProblems.Count > 0)
        {
            return Refused(shapeProblems);
        }

        var executeMethods = useCaseClass.GetMembers(ExecuteMethodName)
            .OfType<IMethodSymbol>()
            .Where(method => method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public && !method.IsStatic)
            .ToArray();
        if (executeMethods.Length == 0)
        {
            return Refused([Problem(GeneratorDiagnostics.NoPublicExecute, classDeclaration.Location, classDeclaration.Name)]);
        }

        var genericExecuteProblems = executeMethods.Where(method => method.IsGenericMethod)
            .Select(method => Problem(GeneratorDiagnostics.GenericExecute, SourceLocation.From(method.Locations.FirstOrDefault()), classDeclaration.Name))
            .ToList();
        if (genericExecuteProblems.Count > 0)
        {
            return Refused(genericExecuteProblems);
        }

        var namespacePrefix = classDeclaration.Namespace == "" ? "" : classDeclaration.Namespace + ".";
        return new UseCaseReadResult
        {
            Declaration = new UseCaseDeclaration
            {
                Class = classDeclaration,
                InterfaceName = interfaceName,
                InterfaceFullyQualifiedName = "global::" + namespacePrefix + interfaceName,
                MarkerInterface = PendingMarkerInterface,
                ExecuteMethods = new EquatableArray<MethodDeclaration>(executeMethods.Select(method => DeclarationReader.ReadMethod(method, cancellationToken))),
            },
            IsSkipped = false,
            Diagnostics = EquatableArray<DiagnosticInfo>.Empty,
        };
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
