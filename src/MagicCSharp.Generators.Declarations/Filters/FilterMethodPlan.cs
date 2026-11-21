namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>What one generated filter method (the repository's <c>ApplyFilter</c> or the filter's <c>Matches</c>) writes and refuses.</summary>
public sealed record FilterMethodPlan
{
    /// <summary>The rules it writes a line for, in the filter's property order.</summary>
    public required EquatableArray<FilterFieldRule> Rules { get; init; }

    /// <summary>The custom fields; when there are any, the method calls its hand-written custom method last.</summary>
    public required EquatableArray<string> CustomFieldNames { get; init; }

    /// <summary>The build errors: a custom field the custom method does not read, a matched field the Dal cannot carry.</summary>
    public required EquatableArray<DiagnosticInfo> Failures { get; init; }
}
