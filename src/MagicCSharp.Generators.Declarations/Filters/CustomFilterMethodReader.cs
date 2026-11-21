using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     Reads which filter fields a hand-written custom filter method reads, from its syntax. A custom field must be read
///     there, so a field added to the filter later fails the build instead of being silently ignored.
/// </summary>
public static class CustomFilterMethodReader
{
    public const string ApplyCustomFilterName = "ApplyCustomFilter";

    public const string MatchesCustomFilterName = "MatchesCustomFilter";

    /// <summary>The repository's <c>ApplyCustomFilter(query, filter)</c>: the fields it reads through its filter parameter.</summary>
    public static CustomFilterMethod ReadApplyCustomFilter(INamedTypeSymbol repositoryClass, CancellationToken cancellationToken)
    {
        var method = FindWrittenMethod(repositoryClass, ApplyCustomFilterName, 2, cancellationToken);
        if (method == null)
        {
            return CustomFilterMethod.None;
        }

        var filterParameterName = method.ParameterList.Parameters[1].Identifier.ValueText;
        var readFieldNames = BodyOf(method).DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(memberAccess => IsAccessOn(memberAccess, filterParameterName))
            .Select(memberAccess => memberAccess.Name.Identifier.ValueText);
        return Written(readFieldNames);
    }

    /// <summary>The filter's <c>MatchesCustomFilter(entity)</c>: the fields it reads as <c>X</c> or <c>this.X</c>.</summary>
    public static CustomFilterMethod ReadMatchesCustomFilter(INamedTypeSymbol filterClass, CancellationToken cancellationToken)
    {
        var method = FindWrittenMethod(filterClass, MatchesCustomFilterName, 1, cancellationToken);
        if (method == null)
        {
            return CustomFilterMethod.None;
        }

        var readFieldNames = BodyOf(method).DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(IsReadOfOwnMember)
            .Select(identifierName => identifierName.Identifier.ValueText);
        return Written(readFieldNames);
    }

    private static MethodDeclarationSyntax? FindWrittenMethod(INamedTypeSymbol type, string methodName, int parameterCount,
        CancellationToken cancellationToken)
    {
        return type.GetMembers(methodName)
            .OfType<IMethodSymbol>()
            .Where(method => method.Parameters.Length == parameterCount)
            .SelectMany(method => method.DeclaringSyntaxReferences)
            .Select(syntaxReference => syntaxReference.GetSyntax(cancellationToken))
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(declaration => declaration.Body != null || declaration.ExpressionBody != null);
    }

    private static SyntaxNode BodyOf(MethodDeclarationSyntax method)
    {
        return (SyntaxNode?)method.Body ?? method.ExpressionBody!;
    }

    private static bool IsAccessOn(MemberAccessExpressionSyntax memberAccess, string variableName)
    {
        var accessedIdentifier = memberAccess.Expression as IdentifierNameSyntax;
        return accessedIdentifier != null && accessedIdentifier.Identifier.ValueText == variableName;
    }

    // A simple name reads the filter's own member unless it is the member part of an access on something else
    // ('entity.Status' reads the entity's Status, not the filter's).
    private static bool IsReadOfOwnMember(IdentifierNameSyntax identifierName)
    {
        var memberAccess = identifierName.Parent as MemberAccessExpressionSyntax;
        if (memberAccess == null || memberAccess.Name != identifierName)
        {
            return true;
        }

        return memberAccess.Expression.IsKind(SyntaxKind.ThisExpression);
    }

    private static CustomFilterMethod Written(IEnumerable<string> readFieldNames)
    {
        return new CustomFilterMethod
        {
            HasBody = true,
            ReadFieldNames = new EquatableArray<string>(readFieldNames.Distinct().OrderBy(name => name, StringComparer.Ordinal)),
        };
    }
}
