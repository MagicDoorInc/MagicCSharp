using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0019: a variable holding one of the codebase's own types is named after the type.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityVariableNameAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0019";

    private const string UseCaseSuffix = "UseCase";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Entity variable must carry the full type-derived name",
        "Variable '{0}' holds a '{1}'; name it '{2}'", "Naming", DiagnosticSeverity.Error, true,
        "A variable created with 'new' or declared by 'foreach' over a class or struct from this codebase or " +
        "from MagicCSharp is named after its type ('var leaseEdit = new LeaseEdit()'). A qualifier in front of the full type name is allowed " +
        "when two of them share a scope ('previousLeaseEdit'). A use case is named for its operation, dropping the 'UseCase' suffix as " +
        "MCS0007 does for dependencies ('var signLease = new SignLeaseUseCase(...)').");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeForEach, SyntaxKind.ForEachStatement);
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
    }

    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context)
    {
        var forEachStatement = (ForEachStatementSyntax)context.Node;
        var local = context.SemanticModel.GetDeclaredSymbol(forEachStatement, context.CancellationToken);
        if (local == null)
        {
            return;
        }

        Check(context, forEachStatement.Identifier, local.Type);
    }

    private static void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (declarator.Initializer == null)
        {
            return;
        }

        var declaration = declarator.Parent as VariableDeclarationSyntax;
        if (declaration == null || !declaration.Type.IsVar)
        {
            return;
        }

        var value = declarator.Initializer.Value;
        var isObjectCreation = value.IsKind(SyntaxKind.ObjectCreationExpression) || value.IsKind(SyntaxKind.ImplicitObjectCreationExpression);
        if (!isObjectCreation)
        {
            return;
        }

        var local = context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) as ILocalSymbol;
        if (local == null)
        {
            return;
        }

        Check(context, declarator.Identifier, local.Type);
    }

    private static void Check(SyntaxNodeAnalysisContext context, SyntaxToken identifier, ITypeSymbol type)
    {
        var namedType = type as INamedTypeSymbol;
        if (namedType == null || namedType.IsGenericType)
        {
            return;
        }

        if (namedType.TypeKind != TypeKind.Class && namedType.TypeKind != TypeKind.Struct)
        {
            return;
        }

        if (!SymbolFacts.IsFirstParty(namedType, context.Compilation))
        {
            return;
        }

        var name = identifier.ValueText;
        var expectedTypeName = ExpectedTypeName(namedType, context.Compilation);
        if (IsTypeDerivedName(name, expectedTypeName))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), name, namedType.Name,
            IdentifierWords.ToCamelCase(expectedTypeName)));
    }

    /// <summary>
    ///     The name a variable holding <paramref name="namedType" /> is built from: the type's own name, or — for a use
    ///     case — the operation it performs, so a local reads the same as the dependency MCS0007 names.
    /// </summary>
    private static string ExpectedTypeName(INamedTypeSymbol namedType, Compilation compilation)
    {
        var magicUseCase = compilation.GetTypeByMetadataName(KnownTypeNames.MagicUseCase);
        var isUseCase = SymbolFacts.Implements(namedType, magicUseCase);
        var hasSuffix = namedType.Name.Length > UseCaseSuffix.Length && namedType.Name.EndsWith(UseCaseSuffix, StringComparison.Ordinal);

        if (isUseCase && hasSuffix)
        {
            return namedType.Name.Substring(0, namedType.Name.Length - UseCaseSuffix.Length);
        }

        return namedType.Name;
    }

    private static bool IsTypeDerivedName(string name, string typeName)
    {
        if (name == IdentifierWords.ToCamelCase(typeName))
        {
            return true;
        }

        var hasQualifier = name.Length > typeName.Length && char.IsLower(name[0]);
        return hasQualifier && name.EndsWith(typeName, StringComparison.Ordinal);
    }
}
