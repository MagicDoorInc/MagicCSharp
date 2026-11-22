using System.Collections.Immutable;
using MagicCSharp.Generators.Declarations;
using MagicCSharp.UseCases;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Tests;

/// <summary>
///     Runs the generators over a subject compilation the way the compiler does, then holds every generated file to
///     the rules all generated code follows and checks the result compiles.
/// </summary>
public static class GeneratorHarness
{
    public const string SubjectAssemblyName = "Acme.Leasing";

    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest);

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new Lazy<ImmutableArray<MetadataReference>>(CreateReferences);

    /// <summary>One file, Subject.cs, with the default marker interface; fails on any generator diagnostic.</summary>
    public static GeneratorRunResult RunClean(string source)
    {
        var generatorRunResult = Run(new GeneratorRunRequest { SourceFiles = [Subject(source)] });
        Assert.Empty(generatorRunResult.Diagnostics);
        return generatorRunResult;
    }

    public static TestSourceFile Subject(string source)
    {
        return new TestSourceFile { Path = "Subject.cs", Text = source };
    }

    public static GeneratorRunResult Run(GeneratorRunRequest request)
    {
        var compilation = CreateCompilation(request.SourceFiles, References.Value.AddRange(request.AdditionalReferences));
        var driver = CreateDriver(request, false);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        var runResult = driver.GetRunResult();

        var generatedFiles = runResult.Results.SelectMany(result => result.GeneratedSources)
            .Select(generatedSource => new TestSourceFile { Path = generatedSource.HintName, Text = generatedSource.SourceText.ToString() })
            .ToArray();
        foreach (var generatedTestSourceFile in generatedFiles)
        {
            AssertFollowsGeneratedCodeRules(generatedTestSourceFile, outputCompilation);
        }

        AssertDeterministic(request, compilation, generatedFiles);
        AssertCompiles(outputCompilation);

        return new GeneratorRunResult
        {
            GeneratedFiles = generatedFiles,
            Diagnostics = runResult.Diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceTree?.FilePath)
                .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .Select(Describe)
                .ToArray(),
            OutputCompilation = outputCompilation,
        };
    }

    public static CSharpCompilation CreateCompilation(TestSourceFile[] sourceFiles)
    {
        return CreateCompilation(sourceFiles, References.Value);
    }

    public static CSharpCompilation CreateCompilation(TestSourceFile[] sourceFiles, IEnumerable<MetadataReference> references)
    {
        var syntaxTrees = sourceFiles.Select(sourceFile => CSharpSyntaxTree.ParseText(sourceFile.Text, ParseOptions, sourceFile.Path));
        return CSharpCompilation.Create(SubjectAssemblyName, syntaxTrees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    public static ImmutableArray<MetadataReference> DefaultReferences()
    {
        return References.Value;
    }

    public static GeneratorDriver CreateDriver(GeneratorRunRequest request, bool isTrackingSteps)
    {
        var globalOptions = new Dictionary<string, string>();
        if (request.MarkerInterfaceSetting != null)
        {
            globalOptions["build_property." + UseCaseMarkerInterfaceSetting.PropertyName] = request.MarkerInterfaceSetting;
        }

        foreach (var buildProperty in request.BuildProperties)
        {
            globalOptions["build_property." + buildProperty.Key] = buildProperty.Value;
        }

        var generators = new IIncrementalGenerator[] { new GenerateInterfaceGenerator() }.Concat(request.OtherGenerators)
            .Select(GeneratorExtensions.AsSourceGenerator);
        return CSharpGeneratorDriver.Create(generators, parseOptions: ParseOptions, optionsProvider: new TestAnalyzerConfigOptionsProvider(globalOptions),
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, isTrackingSteps));
    }

    public static string Describe(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        return $"{diagnostic.Id} {lineSpan.Path}:{lineSpan.StartLinePosition.Line + 1} {diagnostic.GetMessage()}";
    }

    private static void AssertFollowsGeneratedCodeRules(TestSourceFile generatedFile, Compilation outputCompilation)
    {
        Assert.StartsWith(GeneratedFile.Header, generatedFile.Text);
        Assert.DoesNotContain("\r", generatedFile.Text);

        var generatedTree = outputCompilation.SyntaxTrees.Single(syntaxTree => syntaxTree.FilePath.EndsWith(generatedFile.Path, StringComparison.Ordinal));
        var semanticModel = outputCompilation.GetSemanticModel(generatedTree);
        var root = generatedTree.GetRoot();
        Assert.Empty(root.DescendantNodes().OfType<UsingDirectiveSyntax>());
        Assert.Empty(root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>());

        // Only type names must be global::-qualified; a method body also names parameters, locals and inherited
        // members, which bind to no type.
        var unqualifiedNames = root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(simpleName => !IsInNamespaceName(simpleName) && !IsGlobalAlias(simpleName) && !StartsFromGlobal(simpleName) &&
                                 NamesATypeOrNamespace(simpleName, semanticModel))
            .Select(simpleName => simpleName.ToString())
            .ToArray();
        Assert.True(unqualifiedNames.Length == 0, $"{generatedFile.Path} names types without global::: {string.Join(", ", unqualifiedNames)}");
    }

    private static void AssertDeterministic(GeneratorRunRequest request, Compilation compilation, TestSourceFile[] generatedFiles)
    {
        var secondDriver = CreateDriver(request, false).RunGenerators(compilation);
        var secondTexts = secondDriver.GetRunResult()
            .Results.SelectMany(result => result.GeneratedSources)
            .Select(generatedSource => generatedSource.HintName + "\n" + generatedSource.SourceText)
            .ToArray();
        Assert.Equal(generatedFiles.Select(generatedFile => generatedFile.Path + "\n" + generatedFile.Text), secondTexts);
    }

    private static void AssertCompiles(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(Describe).ToArray();
        Assert.Empty(errors);
    }

    // A method's type parameter and the notnull and unmanaged constraints are names that global:: cannot qualify.
    private static bool NamesATypeOrNamespace(SimpleNameSyntax simpleName, SemanticModel semanticModel)
    {
        if (simpleName.IsVar || IsConstraintKeyword(simpleName))
        {
            return false;
        }

        var symbol = semanticModel.GetSymbolInfo(simpleName).Symbol;
        if (symbol is ITypeParameterSymbol)
        {
            return false;
        }

        return symbol == null || symbol is ITypeSymbol || symbol is INamespaceSymbol;
    }

    private static bool IsConstraintKeyword(SimpleNameSyntax simpleName)
    {
        var identifier = simpleName.Identifier.Text;
        return simpleName.Parent is TypeConstraintSyntax && (identifier == "notnull" || identifier == "unmanaged");
    }

    private static bool IsInNamespaceName(SyntaxNode node)
    {
        var namespaceDeclaration = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return namespaceDeclaration != null && namespaceDeclaration.Name.Span.Contains(node.Span);
    }

    private static bool IsGlobalAlias(SimpleNameSyntax simpleName)
    {
        var aliasQualifiedName = simpleName.Parent as AliasQualifiedNameSyntax;
        return aliasQualifiedName != null && aliasQualifiedName.Alias == simpleName;
    }

    // A name counts as qualified when the name or member-access chain it sits in starts at global::.
    private static bool StartsFromGlobal(SimpleNameSyntax simpleName)
    {
        SyntaxNode outermost = simpleName;
        while (outermost.Parent is QualifiedNameSyntax || outermost.Parent is AliasQualifiedNameSyntax || outermost.Parent is MemberAccessExpressionSyntax)
        {
            outermost = outermost.Parent;
        }

        var leftmost = outermost;
        while (true)
        {
            var qualifiedName = leftmost as QualifiedNameSyntax;
            var memberAccess = leftmost as MemberAccessExpressionSyntax;
            if (qualifiedName != null)
            {
                leftmost = qualifiedName.Left;
            }
            else if (memberAccess != null)
            {
                leftmost = memberAccess.Expression;
            }
            else
            {
                break;
            }
        }

        var aliasQualifiedName = leftmost as AliasQualifiedNameSyntax;
        return aliasQualifiedName != null && aliasQualifiedName.Alias.Identifier.Text == "global";
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var platformReferences = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(path) == "netstandard.dll" ||
                           Path.GetFileName(path) == "mscorlib.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        return platformReferences.Append(MetadataReference.CreateFromFile(typeof(IMagicUseCase).Assembly.Location)).ToImmutableArray();
    }
}
