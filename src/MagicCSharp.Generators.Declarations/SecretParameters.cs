using MagicCSharp.Analyzers;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Whether a parameter looks like it carries a secret, so writing it into a log would leak it: a name with the whole
///     word password, secret, token or key (<c>newPassword</c>, <c>clientSecret</c>, <c>accessToken</c>,
///     <c>apiKey</c>; not <c>keyboardLayout</c>), or a stream or byte array, whose content no log line should hold.
/// </summary>
public static class SecretParameters
{
    private static readonly string[] SecretWords = { "password", "passwords", "secret", "secrets", "token", "tokens", "key", "keys" };

    public static bool LooksSecret(IParameterSymbol parameter)
    {
        return HasSecretWord(parameter.Name) || CarriesRawContent(parameter.Type);
    }

    public static bool HasSecretWord(string parameterName)
    {
        return IdentifierWords.Split(parameterName)
            .Select(word => word.Trim('_').ToLowerInvariant())
            .Any(word => SecretWords.Contains(word));
    }

    private static bool CarriesRawContent(ITypeSymbol type)
    {
        var arrayType = type as IArrayTypeSymbol;
        if (arrayType != null)
        {
            return arrayType.ElementType.SpecialType == SpecialType.System_Byte;
        }

        for (ITypeSymbol? baseType = type; baseType != null; baseType = baseType.BaseType)
        {
            if (baseType.Name == "Stream" && baseType.ContainingNamespace?.ToDisplayString() == "System.IO")
            {
                return true;
            }
        }

        return false;
    }
}
