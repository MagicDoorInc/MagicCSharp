namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>Writes a repository's generated <c>ApplyFilter</c> from its <see cref="FilterMethodPlan" />.</summary>
public static class FilterQueryWriter
{
    private const string Queryable = "global::System.Linq.IQueryable";

    public static GeneratedFile WriteFile(FilteredRepositoryDeclaration repository, FilterMethodPlan plan)
    {
        var queryType = Queryable + "<" + repository.Dal.FullyQualifiedName + ">";
        var filterParameters = queryType + " query, " + repository.Filter.FullyQualifiedName + " filter";
        var hasCustomFields = plan.CustomFieldNames.Count > 0;

        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();
        if (repository.Repository.Namespace != "")
        {
            sourceWriter.WriteLine("namespace " + repository.Repository.Namespace + ";");
            sourceWriter.WriteLine();
        }

        sourceWriter.WriteLine("partial class " + repository.Repository.Name);
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine(repository.ApplyFilterAccessibility + " override " + queryType + " ApplyFilter(" + filterParameters + ")");
        sourceWriter.OpenBlock();
        foreach (var filterFieldRule in plan.Rules)
        {
            sourceWriter.WriteLine("query = " + HelperCall(filterFieldRule, repository.Dal.FullyQualifiedName) + ";");
        }

        if (hasCustomFields)
        {
            sourceWriter.WriteLine("query = " + CustomFilterMethodReader.ApplyCustomFilterName + "(query, filter);");
        }

        sourceWriter.WriteLine("return query;");
        sourceWriter.CloseBlock();

        if (hasCustomFields)
        {
            sourceWriter.WriteLine();
            sourceWriter.WriteLine("private partial " + queryType + " " + CustomFilterMethodReader.ApplyCustomFilterName + "(" + filterParameters + ");");
        }

        sourceWriter.CloseBlock();

        return new GeneratedFile
        {
            HintName = HintNames.For(repository.Repository, "FilterQuery"),
            Text = sourceWriter.ToString(),
        };
    }

    // A static call with explicit type arguments: no consumer extension method of the same name can make it ambiguous,
    // and the lambda converts the column to the helper's nullable form.
    private static string HelperCall(FilterFieldRule filterFieldRule, string dalTypeName)
    {
        var typeArguments = filterFieldRule.IsStringList
            ? dalTypeName
            : dalTypeName + ", " + filterFieldRule.ValueTypeName;
        return FilterTypeNames.QueryFilterExtensions + ".Apply" + filterFieldRule.Kind + "Filter<" + typeArguments + ">(query, filter." + filterFieldRule.FieldName +
               ", row => row." + filterFieldRule.PropertyName + ")";
    }
}
