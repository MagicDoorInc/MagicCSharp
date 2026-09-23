using System.Text;

namespace MagicCSharp.Analyzers;

internal static class IdentifierWords
{
    public static List<string> Split(string identifier)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        for (var index = 0; index < identifier.Length; index++)
        {
            if (current.Length > 0 && StartsNewWord(identifier, index))
            {
                words.Add(current.ToString());
                current.Clear();
            }

            current.Append(identifier[index]);
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }

    public static string First(string identifier)
    {
        if (identifier.Length == 0)
        {
            return identifier;
        }

        var end = 1;
        while (end < identifier.Length && !StartsNewWord(identifier, end))
        {
            end++;
        }

        return identifier.Substring(0, end);
    }

    public static string ToCamelCase(string value)
    {
        var leadingLength = 0;
        while (leadingLength < value.Length && !char.IsLower(value[leadingLength]))
        {
            leadingLength++;
        }

        if (leadingLength == value.Length)
        {
            return value.ToLowerInvariant();
        }

        var lowercasedLength = leadingLength <= 1 ? leadingLength : leadingLength - 1;
        if (lowercasedLength == 0)
        {
            return value;
        }

        return value.Substring(0, lowercasedLength).ToLowerInvariant() + value.Substring(lowercasedLength);
    }

    private static bool StartsNewWord(string identifier, int index)
    {
        var previous = identifier[index - 1];
        var current = identifier[index];
        var isPreviousDigit = char.IsDigit(previous);
        var isCurrentDigit = char.IsDigit(current);
        if (isPreviousDigit != isCurrentDigit)
        {
            return true;
        }

        if (isCurrentDigit)
        {
            return false;
        }

        var isPreviousUpper = char.IsUpper(previous);
        var isCurrentUpper = char.IsUpper(current);
        if (!isPreviousUpper && isCurrentUpper)
        {
            return true;
        }

        var isNextLower = index + 1 < identifier.Length && char.IsLower(identifier[index + 1]);
        return isPreviousUpper && isCurrentUpper && isNextLower;
    }
}
