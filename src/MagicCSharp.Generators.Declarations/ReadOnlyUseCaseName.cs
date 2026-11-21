namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Whether a use case's class name says it only reads: after any caller prefix (<c>External</c>, <c>Internal</c>,
///     and the extra ones a codebase names), it starts with a read-only verb as a whole word. <c>ExternalGetLease</c>
///     reads; <c>Listing…</c> and <c>Isolate…</c> do not start with <c>List</c> or <c>Is</c>.
///     <para>
///         One rule for the MCS0015 analyzer and the <c>[LogExecuting]</c> generator, compiled into both. Each passes
///         its own verbs: MCS0015 keeps its three, so its existing users' builds do not change.
///     </para>
/// </summary>
public static class ReadOnlyUseCaseName
{
    private static readonly string[] BuiltInCallerPrefixes = { "External", "Internal" };

    /// <param name="className">The use case class's name, <c>ExternalGetLeaseUseCase</c>.</param>
    /// <param name="readOnlyVerbs">The verbs that mark a read: <c>Get</c>, <c>List</c>, …</param>
    /// <param name="extraCallerPrefixes">Caller prefixes besides <c>External</c> and <c>Internal</c>.</param>
    public static bool IsReadOnly(string className, IReadOnlyList<string> readOnlyVerbs, IReadOnlyList<string> extraCallerPrefixes)
    {
        var callerPrefixes = BuiltInCallerPrefixes.Concat(extraCallerPrefixes).ToArray();
        var remainder = className;
        var hasStrippedPrefix = true;
        while (hasStrippedPrefix)
        {
            hasStrippedPrefix = false;
            foreach (var callerPrefix in callerPrefixes)
            {
                if (StartsWithWord(remainder, callerPrefix))
                {
                    remainder = remainder.Substring(callerPrefix.Length);
                    hasStrippedPrefix = true;
                }
            }
        }

        return readOnlyVerbs.Any(readOnlyVerb => StartsWithWord(remainder, readOnlyVerb));
    }

    private static bool StartsWithWord(string value, string word)
    {
        if (word.Length == 0 || !value.StartsWith(word, StringComparison.Ordinal))
        {
            return false;
        }

        return value.Length == word.Length || char.IsUpper(value[word.Length]);
    }
}
