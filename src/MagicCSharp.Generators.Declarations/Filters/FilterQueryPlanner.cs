using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     Plans a repository's generated <c>ApplyFilter</c>: the <see cref="FilterRulePlanner" /> rules, each over the Dal
///     column of its entity property. A matched field whose column is missing or of another type is refused rather than
///     left to hand-written code, so the query and the filter's <c>Matches</c> (which cannot see the Dal) always treat
///     a field the same way.
/// </summary>
public static class FilterQueryPlanner
{
    public static FilterMethodPlan Plan(FilteredRepositoryDeclaration repository)
    {
        var filterPlan = FilterRulePlanner.Plan(repository.FilterFields, repository.EntityProperties);
        var entityProperties = repository.EntityProperties.ToDictionary(property => property.Name);
        var dalProperties = repository.DalProperties.Where(property => property.IsReadable).ToDictionary(property => property.Name);

        var writtenRules = new List<FilterFieldRule>();
        var failures = new List<DiagnosticInfo>();
        foreach (var filterFieldRule in filterPlan.Rules)
        {
            var entityProperty = entityProperties[filterFieldRule.PropertyName];
            if (!dalProperties.TryGetValue(filterFieldRule.PropertyName, out var dalProperty))
            {
                failures.Add(Failure(FilterDiagnostics.MatchedFieldWithoutDalColumn, repository,
                    [repository.Filter.Name, filterFieldRule.FieldName, repository.Entity.Name, filterFieldRule.PropertyName, repository.Dal.Name]));
            }
            else if (dalProperty.TypeName != entityProperty.TypeName)
            {
                failures.Add(Failure(FilterDiagnostics.MatchedFieldDalColumnTypeDiffers, repository,
                [
                    repository.Filter.Name, filterFieldRule.FieldName, repository.Entity.Name, filterFieldRule.PropertyName, repository.Dal.Name, entityProperty.TypeName,
                    dalProperty.TypeName,
                ]));
            }
            else
            {
                writtenRules.Add(filterFieldRule);
            }
        }

        foreach (var customFieldName in filterPlan.CustomFieldNames.Where(name => !repository.ApplyCustomFilter.ReadFieldNames.Contains(name)))
        {
            failures.Add(Failure(FilterDiagnostics.CustomFieldNotReadByApplyCustomFilter, repository,
                [repository.Filter.Name, customFieldName, repository.Entity.Name, repository.Repository.Name]));
        }

        return new FilterMethodPlan
        {
            Rules = new EquatableArray<FilterFieldRule>(writtenRules),
            CustomFieldNames = filterPlan.CustomFieldNames,
            Failures = new EquatableArray<DiagnosticInfo>(failures),
        };
    }

    private static DiagnosticInfo Failure(DiagnosticDescriptor descriptor, FilteredRepositoryDeclaration repository,
        string[] messageArguments)
    {
        return new DiagnosticInfo
        {
            Descriptor = descriptor,
            Location = repository.Repository.Location,
            MessageArguments = new EquatableArray<string>(messageArguments),
        };
    }
}
