using MagicCSharp.Generators.Declarations.Dals;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     The name rules that match filter fields to entity properties. The repository's query and the filter's
///     <c>Matches</c> are both planned here, against the entity, so they always pick the same fields and the same rule.
///     <list type="bullet">
///         <item><c>List</c>: a list field named as the plural of a property whose type is the element or its nullable form (<c>Ids</c> → <c>Id</c>).</item>
///         <item><c>NullableValue</c>: a nullable struct field named as a property of that type or its nullable form (<c>TenantId</c>).</item>
///         <item><c>NullablePresence</c>: a <c>bool? Is{X}</c> field, when the entity has no <c>Is{X}</c>, over a nullable struct property <c>{X}</c> (<c>IsDeleted</c> → <c>Deleted</c>).</item>
///         <item><c>ComparableRange</c>: a range field named <c>{X}</c> or <c>{X}Range</c> over a property <c>{X}</c> of the range's type or its nullable form.</item>
///     </list>
///     Every other field is a custom field. Nothing configures a rule.
/// </summary>
public static class FilterRulePlanner
{
    private const string PresencePrefix = "Is";

    private const string RangeSuffix = "Range";

    public static FilterPlan Plan(EquatableArray<FilterField> fields, EquatableArray<MappableProperty> entityProperties)
    {
        var readableProperties = entityProperties.Where(property => property.IsReadable).ToDictionary(property => property.Name);
        var rules = new List<FilterFieldRule>();
        var customFieldNames = new List<string>();
        foreach (var filterField in fields)
        {
            var filterFieldRule = MatchField(filterField, readableProperties);
            if (filterFieldRule == null)
            {
                customFieldNames.Add(filterField.Name);
            }
            else
            {
                rules.Add(filterFieldRule);
            }
        }

        return new FilterPlan
        {
            Rules = new EquatableArray<FilterFieldRule>(rules),
            CustomFieldNames = new EquatableArray<string>(customFieldNames),
        };
    }

    /// <summary>
    ///     The singular names a plural list field may stand for, in the order they are tried:
    ///     <c>…ies</c> → <c>…y</c>, <c>…es</c> → <c>…</c>, <c>…s</c> → <c>…</c>.
    /// </summary>
    public static IReadOnlyList<string> SingularNamesOf(string pluralName)
    {
        var singularNames = new List<string>();
        AddWithoutSuffix(singularNames, pluralName, "ies", "y");
        AddWithoutSuffix(singularNames, pluralName, "es", "");
        AddWithoutSuffix(singularNames, pluralName, "s", "");
        return singularNames;
    }

    private static FilterFieldRule? MatchField(FilterField field, Dictionary<string, MappableProperty> properties)
    {
        switch (field.Shape)
        {
            case FilterFieldShape.List:
                return MatchList(field, properties);
            case FilterFieldShape.NullableValue:
                return MatchNullableValue(field, properties) ?? MatchNullablePresence(field, properties);
            case FilterFieldShape.ComparableRange:
                return MatchComparableRange(field, properties);
            default:
                return null;
        }
    }

    private static FilterFieldRule? MatchList(FilterField field, Dictionary<string, MappableProperty> properties)
    {
        var singularName = SingularNamesOf(field.Name).FirstOrDefault(properties.ContainsKey);
        return MatchValueProperty(field, singularName, FilterRuleKind.List, properties);
    }

    private static FilterFieldRule? MatchNullableValue(FilterField field, Dictionary<string, MappableProperty> properties)
    {
        return MatchValueProperty(field, field.Name, FilterRuleKind.NullableValue, properties);
    }

    private static FilterFieldRule? MatchNullablePresence(FilterField field, Dictionary<string, MappableProperty> properties)
    {
        var isPresenceShaped = field.IsBooleanValue && field.Name.Length > PresencePrefix.Length &&
                               field.Name.StartsWith(PresencePrefix, StringComparison.Ordinal) && !properties.ContainsKey(field.Name);
        if (!isPresenceShaped)
        {
            return null;
        }

        var propertyName = field.Name.Substring(PresencePrefix.Length);
        if (!properties.TryGetValue(propertyName, out var property) || !property.IsNullableValueType)
        {
            return null;
        }

        return new FilterFieldRule
        {
            FieldName = field.Name,
            PropertyName = propertyName,
            Kind = FilterRuleKind.NullablePresence,
            ValueTypeName = property.TypeName.TrimEnd('?'),
            IsStringList = false,
        };
    }

    private static FilterFieldRule? MatchComparableRange(FilterField field, Dictionary<string, MappableProperty> properties)
    {
        var propertyName = properties.ContainsKey(field.Name) ? field.Name : WithoutRangeSuffix(field.Name);
        return MatchValueProperty(field, propertyName, FilterRuleKind.ComparableRange, properties);
    }

    // The helpers take the property as the field's value type or its nullable form; any other type is no match.
    private static FilterFieldRule? MatchValueProperty(FilterField field, string? propertyName, FilterRuleKind kind,
        Dictionary<string, MappableProperty> properties)
    {
        if (propertyName == null || !properties.TryGetValue(propertyName, out var property))
        {
            return null;
        }

        var valueTypeName = field.ValueTypeName!;
        if (property.TypeName != valueTypeName && property.TypeName != valueTypeName + "?")
        {
            return null;
        }

        return new FilterFieldRule
        {
            FieldName = field.Name,
            PropertyName = propertyName,
            Kind = kind,
            ValueTypeName = valueTypeName,
            IsStringList = kind == FilterRuleKind.List && field.IsStringValue,
        };
    }

    private static string? WithoutRangeSuffix(string fieldName)
    {
        var hasRangeSuffix = fieldName.Length > RangeSuffix.Length && fieldName.EndsWith(RangeSuffix, StringComparison.Ordinal);
        return hasRangeSuffix ? fieldName.Substring(0, fieldName.Length - RangeSuffix.Length) : null;
    }

    private static void AddWithoutSuffix(List<string> singularNames, string pluralName, string suffix, string replacement)
    {
        if (pluralName.Length > suffix.Length && pluralName.EndsWith(suffix, StringComparison.Ordinal))
        {
            singularNames.Add(pluralName.Substring(0, pluralName.Length - suffix.Length) + replacement);
        }
    }
}
