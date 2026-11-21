using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Recognises a hand-written <c>Executing:</c> line: a <c>Log…</c> call whose message starts with
///     <c>Executing:</c>, as a literal or an interpolated string. One rule for the MCS0015 analyzer and the
///     <c>[LogExecuting]</c> generator, compiled into both.
/// </summary>
public static class ExecutingLineSyntax
{
    public const string ExecutingPrefix = "Executing:";

    public static bool IsExecutingLine(InvocationExpressionSyntax invocation)
    {
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null || !memberAccess.Name.Identifier.ValueText.StartsWith("Log", StringComparison.Ordinal))
        {
            return false;
        }

        return invocation.ArgumentList.Arguments.Any(argument => StartsWithExecuting(argument.Expression));
    }

    private static bool StartsWithExecuting(ExpressionSyntax expression)
    {
        var literal = expression as LiteralExpressionSyntax;
        if (literal != null)
        {
            return literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText.StartsWith(ExecutingPrefix, StringComparison.Ordinal);
        }

        var interpolated = expression as InterpolatedStringExpressionSyntax;
        if (interpolated == null || interpolated.Contents.Count == 0)
        {
            return false;
        }

        var firstText = interpolated.Contents[0] as InterpolatedStringTextSyntax;
        return firstText != null && firstText.TextToken.ValueText.StartsWith(ExecutingPrefix, StringComparison.Ordinal);
    }
}
