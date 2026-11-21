using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Reads a <c>[GenerateFakeBase]</c> class into the members its fake base stubs, or the reasons it gets none.</summary>
internal static class FakeBaseReader
{
    private static readonly SymbolDisplayFormat ShortTypeFormat = SymbolDisplayFormat.MinimallyQualifiedFormat;

    public static ClientGenerationResult Read(string classMetadataName, Compilation compilation, CancellationToken cancellationToken)
    {
        var fakeBaseClass = compilation.Assembly.GetTypeByMetadataName(classMetadataName);
        if (fakeBaseClass == null)
        {
            return ClientGenerationResult.Nothing;
        }

        var classDeclaration = DeclarationReader.ReadType(fakeBaseClass, cancellationToken);
        var problems = ShapeProblems(classDeclaration);
        var generateFakeBaseAttribute = fakeBaseClass.GetAttributes()
            .First(attribute => attribute.AttributeClass != null &&
                                attribute.AttributeClass.ToDisplayString() == ClientPipelines.GenerateFakeBaseAttributeMetadataName);
        var fakedType = generateFakeBaseAttribute.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
        if (fakedType == null || fakedType.TypeKind != TypeKind.Interface)
        {
            var typeName = fakedType == null ? "null" : fakedType.ToDisplayString();
            problems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.FakeBaseTypeNotInterface, classDeclaration.Location, classDeclaration.Name, typeName));
            return ClientGenerationResult.Refused(problems);
        }

        var stubbedMembers = StubbedMembers(fakeBaseClass, fakedType).ToList();
        foreach (var genericMethod in stubbedMembers.OfType<IMethodSymbol>().Where(method => method.IsGenericMethod))
        {
            problems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.FakeBaseInterfaceHasGenericMethod, classDeclaration.Location, classDeclaration.Name,
                genericMethod.ContainingType.Name, genericMethod.Name));
        }

        if (problems.Count > 0)
        {
            return ClientGenerationResult.Refused(problems);
        }

        var fakeBaseDeclaration = new FakeBaseDeclaration
        {
            Class = classDeclaration,
            InterfaceFullyQualifiedName = DeclarationReader.ReadTypeReference(fakedType).FullyQualifiedName,
            Members = new EquatableArray<FakeMember>(stubbedMembers.Select(member => ReadMember(member, cancellationToken))),
        };
        return ClientGenerationResult.Generated(FakeBaseWriter.WriteFile(fakeBaseDeclaration));
    }

    private static List<DiagnosticInfo> ShapeProblems(TypeDeclaration classDeclaration)
    {
        var shapeProblems = new List<DiagnosticInfo>();
        if (classDeclaration.IsNested)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.NestedFakeBase, classDeclaration.Location, classDeclaration.Name));
        }

        if (!classDeclaration.IsPartial)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.FakeBaseNotPartial, classDeclaration.Location, classDeclaration.Name));
        }

        if (!classDeclaration.IsAbstract)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.FakeBaseNotAbstract, classDeclaration.Location, classDeclaration.Name));
        }

        return shapeProblems;
    }

    // Every abstract instance member of the interface and the interfaces it extends, once, that the class and its
    // bases do not already declare.
    private static IEnumerable<ISymbol> StubbedMembers(INamedTypeSymbol fakeBaseClass, INamedTypeSymbol fakedType)
    {
        var seenSignatures = new HashSet<string>();
        var interfaces = new[] { fakedType }.Concat(fakedType.AllInterfaces);
        foreach (var member in interfaces.SelectMany(fakedInterface => fakedInterface.GetMembers()))
        {
            var isStubbable = !member.IsStatic && member.IsAbstract && IsStubbableKind(member);
            if (isStubbable && !IsDeclaredByClass(fakeBaseClass, member) && seenSignatures.Add(Signature(member)))
            {
                yield return member;
            }
        }
    }

    private static bool IsStubbableKind(ISymbol member)
    {
        var method = member as IMethodSymbol;
        if (method != null)
        {
            return method.MethodKind == MethodKind.Ordinary;
        }

        return member is IPropertySymbol || member is IEventSymbol;
    }

    private static bool IsDeclaredByClass(INamedTypeSymbol fakeBaseClass, ISymbol member)
    {
        for (INamedTypeSymbol? type = fakeBaseClass; type != null; type = type.BaseType)
        {
            var isDeclared = type.GetMembers(member.Name)
                .Any(declared => !declared.IsStatic && declared.DeclaredAccessibility == Accessibility.Public && Signature(declared) == Signature(member));
            if (isDeclared)
            {
                return true;
            }
        }

        return false;
    }

    // Name and parameter types, without nullability: what makes two members the same member of a class.
    private static string Signature(ISymbol member)
    {
        var parameters = ParametersOf(member);
        var parameterTypes = parameters.Select(parameter => parameter.RefKind + " " + parameter.Type.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString());
        return member.Kind + " " + member.Name + "(" + string.Join(", ", parameterTypes) + ")";
    }

    private static IEnumerable<IParameterSymbol> ParametersOf(ISymbol member)
    {
        var method = member as IMethodSymbol;
        if (method != null)
        {
            return method.Parameters;
        }

        var property = member as IPropertySymbol;
        if (property != null)
        {
            return property.Parameters;
        }

        return [];
    }

    private static FakeMember ReadMember(ISymbol member, CancellationToken cancellationToken)
    {
        var interfaceName = member.ContainingType.Name;
        var method = member as IMethodSymbol;
        if (method != null)
        {
            var methodDeclaration = DeclarationReader.ReadMethod(method, cancellationToken);
            return new FakeMember
            {
                Kind = FakeMemberKind.Method,
                Name = UseCaseInterfaceWriter.EscapedName(method.Name),
                TypeFullyQualifiedName = methodDeclaration.ReturnType.FullyQualifiedName,
                ParameterList = UseCaseInterfaceWriter.ParameterList(methodDeclaration),
                HasGetter = false,
                SetterKind = SetterKind.None,
                ScriptedName = interfaceName + "." + method.Name + "(" + ShortParameterTypes(method.Parameters) + ")",
            };
        }

        var property = member as IPropertySymbol;
        if (property != null)
        {
            var propertyDeclaration = DeclarationReader.ReadProperty(property, cancellationToken);
            var getter = property.GetMethod;
            return new FakeMember
            {
                Kind = property.IsIndexer ? FakeMemberKind.Indexer : FakeMemberKind.Property,
                Name = UseCaseInterfaceWriter.EscapedName(property.Name),
                TypeFullyQualifiedName = propertyDeclaration.Type.FullyQualifiedName,
                ParameterList = property.IsIndexer && getter != null ? UseCaseInterfaceWriter.ParameterList(DeclarationReader.ReadMethod(getter, cancellationToken)) : "",
                HasGetter = propertyDeclaration.HasGetter,
                SetterKind = propertyDeclaration.SetterKind,
                ScriptedName = interfaceName + "." + (property.IsIndexer ? "this[" + ShortParameterTypes(property.Parameters) + "]" : property.Name),
            };
        }

        var eventSymbol = (IEventSymbol)member;
        return new FakeMember
        {
            Kind = FakeMemberKind.Event,
            Name = UseCaseInterfaceWriter.EscapedName(eventSymbol.Name),
            TypeFullyQualifiedName = DeclarationReader.ReadTypeReference(eventSymbol.Type).FullyQualifiedName,
            ParameterList = "",
            HasGetter = false,
            SetterKind = SetterKind.None,
            ScriptedName = interfaceName + "." + eventSymbol.Name,
        };
    }

    private static string ShortParameterTypes(IEnumerable<IParameterSymbol> parameters)
    {
        return string.Join(", ", parameters.Select(parameter => parameter.Type.ToDisplayString(ShortTypeFormat)));
    }
}
