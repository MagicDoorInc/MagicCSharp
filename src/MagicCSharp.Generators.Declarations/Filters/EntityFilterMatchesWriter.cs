namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>Writes a filter's generated <c>Matches</c> from its <see cref="FilterMethodPlan" />.</summary>
public static class EntityFilterMatchesWriter
{
    public static GeneratedFile WriteFile(EntityFilterDeclaration filter, FilterMethodPlan plan)
    {
        var entityParameter = filter.Entity.FullyQualifiedName + " entity";
        var hasCustomFields = plan.CustomFieldNames.Count > 0;

        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();
        if (filter.Filter.Namespace != "")
        {
            sourceWriter.WriteLine("namespace " + filter.Filter.Namespace + ";");
            sourceWriter.WriteLine();
        }

        var typeKeyword = filter.Filter.Kind == TypeReferenceKind.Record ? "record" : "class";
        sourceWriter.WriteLine("partial " + typeKeyword + " " + filter.Filter.Name);
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine("public bool Matches(" + entityParameter + ")");
        sourceWriter.OpenBlock();
        foreach (var filterFieldRule in plan.Rules)
        {
            sourceWriter.WriteLine("if (!" + MatchingCall(filterFieldRule) + ")");
            sourceWriter.OpenBlock();
            sourceWriter.WriteLine("return false;");
            sourceWriter.CloseBlock();
            sourceWriter.WriteLine();
        }

        sourceWriter.WriteLine(hasCustomFields ? "return " + CustomFilterMethodReader.MatchesCustomFilterName + "(entity);" : "return true;");
        sourceWriter.CloseBlock();

        if (hasCustomFields)
        {
            sourceWriter.WriteLine();
            sourceWriter.WriteLine("private partial bool " + CustomFilterMethodReader.MatchesCustomFilterName + "(" + entityParameter + ");");
        }

        sourceWriter.CloseBlock();

        return new GeneratedFile
        {
            HintName = HintNames.For(filter.Filter, "FilterMatches"),
            Text = sourceWriter.ToString(),
        };
    }

    private static string MatchingCall(FilterFieldRule filterFieldRule)
    {
        var typeArguments = filterFieldRule.Kind == FilterRuleKind.List && filterFieldRule.ValueTypeName == "string" ? "" : "<" + filterFieldRule.ValueTypeName + ">";
        return FilterTypeNames.FilterMatching + ".Matches" + filterFieldRule.Kind + "Filter" + typeArguments + "(" + filterFieldRule.FieldName + ", entity." + filterFieldRule.PropertyName + ")";
    }
}
