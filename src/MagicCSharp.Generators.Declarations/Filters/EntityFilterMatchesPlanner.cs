namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>Plans a filter's generated <c>Matches</c>: the <see cref="FilterRulePlanner" /> rules, and its custom fields' check.</summary>
public static class EntityFilterMatchesPlanner
{
    public static FilterMethodPlan Plan(EntityFilterDeclaration filter)
    {
        var filterPlan = FilterRulePlanner.Plan(filter.FilterFields, filter.EntityProperties);
        var failures = filterPlan.CustomFieldNames.Where(name => !filter.MatchesCustomFilter.ReadFieldNames.Contains(name))
            .Select(customFieldName => new DiagnosticInfo
            {
                Descriptor = FilterDiagnostics.CustomFieldNotReadByMatchesCustomFilter,
                Location = filter.Filter.Location,
                MessageArguments = new EquatableArray<string>([filter.Filter.Name, customFieldName, filter.Entity.Name]),
            });

        return new FilterMethodPlan
        {
            Rules = filterPlan.Rules,
            CustomFieldNames = filterPlan.CustomFieldNames,
            Failures = new EquatableArray<DiagnosticInfo>(failures),
        };
    }
}
