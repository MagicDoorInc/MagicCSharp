namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>What the name rules make of a filter against its entity: the same for the query and for <c>Matches</c>.</summary>
public sealed record FilterPlan
{
    /// <summary>The matched fields, in the filter's property order.</summary>
    public required EquatableArray<FilterFieldRule> Rules { get; init; }

    /// <summary>The fields no rule matches, in the filter's property order; hand-written code filters them.</summary>
    public required EquatableArray<string> CustomFieldNames { get; init; }
}
