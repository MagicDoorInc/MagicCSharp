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
        var compilation = CreateCompilation(request.SourceFiles, References.Value);
        var driver = CreateDriver(request, false);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        var runResult = driver.GetRunResult();

        var generatedFiles = runResult.Results.SelectMany(result => result.GeneratedSources)
            .Select(generatedSource => new TestSourceFile { Path = generatedSource.HintName, Text = generatedSource.SourceText.ToString() })
            .ToArray();
        foreach (var generatedTestSourceFile in generatedFiles)
        {
            AssertFollowsGeneratedCodeRules(generatedTestSourceFile);
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

    private static void AssertFollowsGeneratedCodeRules(TestSourceFile generatedFile)
    {
        Assert.StartsWith(GeneratedFile.Header, generatedFile.Text);
        Assert.DoesNotContain("\r", generatedFile.Text);

        var root = CSharpSyntaxTree.ParseText(generatedFile.Text, ParseOptions).GetRoot();
        Assert.Empty(root.DescendantNodes().OfType<UsingDirectiveSyntax>());
        Assert.Empty(root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>());

        var unqualifiedNames = root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(simpleName => !IsInNamespaceName(simpleName) && !IsGlobalAlias(simpleName) && !StartsFromGlobal(simpleName))
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
