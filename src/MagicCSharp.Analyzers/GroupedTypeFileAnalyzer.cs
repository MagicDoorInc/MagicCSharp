using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0013: every other public type in a file supports the one the file is named for.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GroupedTypeFileAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0013";
    private const string UseCaseTypeSuffix = "UseCase";

    private static readonly string[] ExemptFileNames = { "GlobalUsings", "Enums" };
    private static readonly string[] UseCaseModelSuffixes = { "Request", "Result", "Response", "Payload", "Context" };
    private static readonly string[] EntityCompanionSuffixes = { "Edit", "Simple", "Filter", "Dal" };

    private static readonly DiagnosticDescriptor UnsupportedTypeRule = new DiagnosticDescriptor(DiagnosticId,
        "A file's non-primary types must support its primary type", "'{0}' does not support '{1}'; move '{1}' to {1}.cs", "Style", DiagnosticSeverity.Error,
        true,
        "A file declares one primary type — the public type whose name matches the file name. Every other " +
        "public type in the file must be a supporting type of that primary (derives from it, is referenced " +
        "by it, is its request/result model, or is its Edit/Simple/Filter/Dal companion); anything else " +
        "belongs in its own file. A use-case interface, its implementation and its request, result and enum " + "types stay together.");

    private static readonly DiagnosticDescriptor JunkDrawerRule = new DiagnosticDescriptor(DiagnosticId, "A file must be named for the type it declares",
        "File '{0}.cs' groups unrelated types; '{1}' belongs in its own file named for it", "Style", DiagnosticSeverity.Error, true,
        "No public type in this file matches the file name, and the file declares more than one public type " +
        "— a junk-drawer file. Each type belongs in its own file named for it.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(UnsupportedTypeRule, JunkDrawerRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSemanticModelAction(Analyze);
    }

    private static void Analyze(SemanticModelAnalysisContext context)
    {
        var tree = context.SemanticModel.SyntaxTree;
        var fileName = Path.GetFileNameWithoutExtension(tree.FilePath);
        if (fileName.EndsWith("Extensions", StringComparison.Ordinal) || ExemptFileNames.Contains(fileName))
        {
            return;
        }

        var root = tree.GetRoot(context.CancellationToken);
        var topLevelTypeDeclarations = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(IsTopLevel).ToList();

        var publicTypeDeclarations = topLevelTypeDeclarations.Where(IsPublic).ToList();
        if (publicTypeDeclarations.Count < 2)
        {
            return;
        }

        var declarationsBySymbol = new Dictionary<INamedTypeSymbol, BaseTypeDeclarationSyntax>(SymbolEqualityComparer.Default);
        foreach (var declaration in topLevelTypeDeclarations)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
            if (symbol == null)
            {
                continue;
            }

            declarationsBySymbol[symbol] = declaration;
        }

        var fileLocalTypes = new HashSet<INamedTypeSymbol>(declarationsBySymbol.Keys, SymbolEqualityComparer.Default);

        var publicSymbols = new List<INamedTypeSymbol>();
        foreach (var declaration in publicTypeDeclarations)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
            if (symbol != null)
            {
                publicSymbols.Add(symbol);
            }
        }

        var primary = publicSymbols.FirstOrDefault(symbol => symbol.Name == fileName);
        if (primary == null)
        {
            ReportJunkDrawer(context, fileName, publicTypeDeclarations);
            return;
        }

        var supporting = ComputeSupportingTypes(primary, fileLocalTypes);
        foreach (var candidate in publicSymbols)
        {
            if (candidate.Name == fileName)
            {
                continue;
            }

            if (supporting.Contains(candidate))
            {
                continue;
            }

            var declaration = declarationsBySymbol[candidate];
            context.ReportDiagnostic(Diagnostic.Create(UnsupportedTypeRule, declaration.Identifier.GetLocation(), primary.Name, candidate.Name));
        }
    }

    private static void ReportJunkDrawer(SemanticModelAnalysisContext context, string fileName, List<BaseTypeDeclarationSyntax> publicTypeDeclarations)
    {
        var ordered = publicTypeDeclarations.OrderBy(declaration => declaration.SpanStart).ToList();
        for (var index = 1; index < ordered.Count; index++)
        {
            context.ReportDiagnostic(Diagnostic.Create(JunkDrawerRule, ordered[index].Identifier.GetLocation(), fileName, ordered[index].Identifier.ValueText));
        }
    }

    private static HashSet<INamedTypeSymbol> ComputeSupportingTypes(INamedTypeSymbol primary, HashSet<INamedTypeSymbol> fileLocalTypes)
    {
        var supporting = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { primary };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var candidate in fileLocalTypes)
            {
                if (supporting.Contains(candidate))
                {
                    continue;
                }

                if (IsSupportingType(primary, candidate, supporting))
                {
                    supporting.Add(candidate);
                    changed = true;
                }
            }
        }

        return supporting;
    }

    private static bool IsSupportingType(INamedTypeSymbol primary, INamedTypeSymbol candidate, HashSet<INamedTypeSymbol> supporting)
    {
        if (DerivesFrom(candidate, primary))
        {
            return true;
        }

        if (IsInterfacePairing(primary, candidate))
        {
            return true;
        }

        if (candidate.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (IsUseCaseModel(primary, candidate))
        {
            return true;
        }

        if (IsEntityCompanion(primary, candidate))
        {
            return true;
        }

        foreach (var supportingType in supporting)
        {
            if (ReferencesType(supportingType, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DerivesFrom(INamedTypeSymbol candidate, INamedTypeSymbol primary)
    {
        var current = candidate.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, primary))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsInterfacePairing(INamedTypeSymbol primary, INamedTypeSymbol candidate)
    {
        if (candidate.AllInterfaces.Contains(primary, SymbolEqualityComparer.Default))
        {
            return true;
        }

        if (primary.AllInterfaces.Contains(candidate, SymbolEqualityComparer.Default))
        {
            return true;
        }

        return false;
    }

    private static bool IsUseCaseModel(INamedTypeSymbol primary, INamedTypeSymbol candidate)
    {
        if (!primary.Name.EndsWith(UseCaseTypeSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var baseName = primary.Name.Substring(0, primary.Name.Length - UseCaseTypeSuffix.Length);
        foreach (var suffix in UseCaseModelSuffixes)
        {
            if (candidate.Name == baseName + suffix)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEntityCompanion(INamedTypeSymbol primary, INamedTypeSymbol candidate)
    {
        foreach (var suffix in EntityCompanionSuffixes)
        {
            if (candidate.Name == primary.Name + suffix)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReferencesType(INamedTypeSymbol referencer, INamedTypeSymbol candidate)
    {
        foreach (var member in referencer.GetMembers())
        {
            if (MemberReferencesType(member, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MemberReferencesType(ISymbol member, INamedTypeSymbol candidate)
    {
        var property = member as IPropertySymbol;
        if (property != null)
        {
            return TypeReferences(property.Type, candidate);
        }

        var field = member as IFieldSymbol;
        if (field != null)
        {
            return field.AssociatedSymbol == null && TypeReferences(field.Type, candidate);
        }

        var method = member as IMethodSymbol;
        if (method == null)
        {
            return false;
        }

        foreach (var parameter in method.Parameters)
        {
            if (TypeReferences(parameter.Type, candidate))
            {
                return true;
            }
        }

        return TypeReferences(method.ReturnType, candidate);
    }

    private static bool TypeReferences(ITypeSymbol type, INamedTypeSymbol candidate)
    {
        var arrayType = type as IArrayTypeSymbol;
        if (arrayType != null)
        {
            return TypeReferences(arrayType.ElementType, candidate);
        }

        var namedType = type as INamedTypeSymbol;
        if (namedType == null)
        {
            return false;
        }

        if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && namedType.TypeArguments.Length == 1)
        {
            return TypeReferences(namedType.TypeArguments[0], candidate);
        }

        if (SymbolEqualityComparer.Default.Equals(namedType, candidate))
        {
            return true;
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            if (TypeReferences(typeArgument, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTopLevel(BaseTypeDeclarationSyntax type)
    {
        return type.Parent is CompilationUnitSyntax || type.Parent is BaseNamespaceDeclarationSyntax;
    }

    private static bool IsPublic(BaseTypeDeclarationSyntax type)
    {
        return type.Modifiers.Any(SyntaxKind.PublicKeyword);
    }
}
