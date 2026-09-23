using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers.Tests;

public static class AnalyzerHarness
{
    // An Acme assembly, so Acme.Libraries.Shared is the same codebase split into another project, while
    // AcmeTools — same prefix, different first name segment — is a third party like ThirdParty.Stubs.
    private const string SubjectAssemblyName = "Acme.Leasing";

    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Preview);

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new Lazy<ImmutableArray<MetadataReference>>(CreateReferences);

    public static Task<string[]> Analyze(DiagnosticAnalyzer analyzer, string source)
    {
        return Analyze(analyzer, [new TestSourceFile { Path = "Subject.cs", Text = source }]);
    }

    public static async Task<string[]> Analyze(DiagnosticAnalyzer analyzer, TestSourceFile[] sourceFiles)
    {
        var syntaxTrees = sourceFiles.Select(sourceFile => CSharpSyntaxTree.ParseText(sourceFile.Text, ParseOptions, sourceFile.Path)).ToArray();
        var compilation = CreateCompilation(SubjectAssemblyName, syntaxTrees, References.Value);
        AssertCompiles(compilation);

        var analyzerExceptions = new ConcurrentQueue<string>();
        var analyzerOptions = new CompilationWithAnalyzersOptions(new AnalyzerOptions([]),
            (exception, _, _) => analyzerExceptions.Enqueue(exception.ToString()), false, false);
        var diagnostics = await compilation.WithAnalyzers([analyzer], analyzerOptions).GetAnalyzerDiagnosticsAsync();
        Assert.Empty(analyzerExceptions);

        return diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceTree?.FilePath)
            .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .Select(Describe)
            .ToArray();
    }

    private static string Describe(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        return $"{diagnostic.Id} {lineSpan.Path}:{lineSpan.StartLinePosition.Line + 1} {diagnostic.GetMessage()}";
    }

    private static void AssertCompiles(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(Describe).ToArray();
        Assert.Empty(errors);
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var platformReferences = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(path) == "netstandard.dll" ||
                           Path.GetFileName(path) == "mscorlib.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

        var thirdPartyStubs = EmitReference("ThirdParty.Stubs", KnownTypeStubs.ThirdPartySource, platformReferences);
        var frameworkStubs = EmitReference("MagicCSharp", KnownTypeStubs.FrameworkSource, platformReferences);
        var siblingStubs = EmitReference("Acme.Libraries.Shared", KnownTypeStubs.SiblingSource, platformReferences);
        var lookalikeStubs = EmitReference("AcmeTools", KnownTypeStubs.LookalikeSource, platformReferences);

        return platformReferences.Add(thirdPartyStubs).Add(frameworkStubs).Add(siblingStubs).Add(lookalikeStubs);
    }

    private static MetadataReference EmitReference(string assemblyName, string source, ImmutableArray<MetadataReference> references)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, ParseOptions, assemblyName + ".cs");
        var compilation = CreateCompilation(assemblyName, [syntaxTree], references);
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        Assert.True(emitResult.Success, string.Join("\n", emitResult.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static CSharpCompilation CreateCompilation(string assemblyName, SyntaxTree[] syntaxTrees, IEnumerable<MetadataReference> references)
    {
        return CSharpCompilation.Create(assemblyName, syntaxTrees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }
}
