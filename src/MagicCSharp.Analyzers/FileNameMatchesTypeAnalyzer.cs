using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0014: a file is named after a type it declares.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileNameMatchesTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0014";

    private static readonly string[] ExemptFileNames = { "GlobalUsings", "Enums", "Program" };

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "File name must match the type it declares",
        "File '{0}.cs' declares '{1}'; rename the file or the type so they match", "Style", DiagnosticSeverity.Error, true,
        "The file name is the first thing a reader uses to find a type, so it equals the name of a type " +
        "the file declares; Program.cs, extension and other static classes and enum-only files are exempt.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var fileName = Path.GetFileNameWithoutExtension(context.Tree.FilePath);
        if (fileName.EndsWith("Extensions") || ExemptFileNames.Contains(fileName))
        {
            return;
        }

        var root = context.Tree.GetRoot(context.CancellationToken);
        var topLevelTypes = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(IsTopLevel).ToList();

        if (topLevelTypes.Count == 0)
        {
            return;
        }

        if (topLevelTypes.All(type => type is EnumDeclarationSyntax))
        {
            return;
        }

        var classDeclarations = topLevelTypes.OfType<ClassDeclarationSyntax>().ToList();
        if (topLevelTypes.Count == 1 && classDeclarations.Count == 1 && IsStaticExtensionClass(classDeclarations[0]))
        {
            return;
        }

        if (topLevelTypes.Any(type => type.Identifier.ValueText == fileName))
        {
            return;
        }

        var publicTypes = topLevelTypes.Where(IsPublic).ToList();
        var reported = publicTypes.Count > 0 ? publicTypes[0] : topLevelTypes[0];

        context.ReportDiagnostic(Diagnostic.Create(Rule, reported.Identifier.GetLocation(), fileName, reported.Identifier.ValueText));
    }

    private static bool IsTopLevel(BaseTypeDeclarationSyntax type)
    {
        return type.Parent is CompilationUnitSyntax || type.Parent is BaseNamespaceDeclarationSyntax;
    }

    private static bool IsPublic(BaseTypeDeclarationSyntax type)
    {
        return type.Modifiers.Any(SyntaxKind.PublicKeyword);
    }

    private static bool IsStaticExtensionClass(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword);
    }
}
