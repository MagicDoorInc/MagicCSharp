using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Filters;

/// <summary>
///     The filter query and <c>Matches</c> diagnostics, <c>MCS13xx</c>. A filter field the generated code cannot
///     filter is refused by name, so it is never silently ignored.
/// </summary>
public static class FilterDiagnostics
{
    public static readonly DiagnosticDescriptor CustomFieldNotReadByApplyCustomFilter = new DiagnosticDescriptor("MCS1301",
        "A custom filter field is not read by ApplyCustomFilter",
        "'{0}.{1}' matches no property of '{2}' by the filter name rules, and '{3}.ApplyCustomFilter' does not read 'filter.{1}'; filter it there, or rename it to follow a rule",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor MatchedFieldWithoutDalColumn = new DiagnosticDescriptor("MCS1302",
        "A filter field's property has no Dal column",
        "'{0}.{1}' filters '{2}.{3}', but '{4}' has no public property '{3}' to query; add the column to the Dal, or write ApplyFilter by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor MatchedFieldDalColumnTypeDiffers = new DiagnosticDescriptor("MCS1303",
        "A filter field's Dal column has another type",
        "'{0}.{1}' filters '{2}.{3}' ({5}), but '{4}.{3}' is {6}; give the Dal column the entity's type, or write ApplyFilter by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor CustomFieldNotReadByMatchesCustomFilter = new DiagnosticDescriptor("MCS1304",
        "A custom filter field is not read by MatchesCustomFilter",
        "'{0}.{1}' matches no property of '{2}' by the filter name rules, and '{0}.MatchesCustomFilter' does not read '{1}'; match it there, or rename it to follow a rule",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NestedFilteredRepository = new DiagnosticDescriptor("MCS1305",
        "A nested repository gets no generated ApplyFilter",
        "'{0}' is nested in another type; move it to the top level of its file, or write ApplyFilter by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericFilteredRepository = new DiagnosticDescriptor("MCS1306",
        "A generic repository gets no generated ApplyFilter",
        "'{0}' is generic; write ApplyFilter by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NestedEntityFilter = new DiagnosticDescriptor("MCS1307",
        "A nested filter gets no generated Matches",
        "'{0}' is nested in another type; move it to the top level of its file, or write Matches by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericEntityFilter = new DiagnosticDescriptor("MCS1308",
        "A generic filter gets no generated Matches",
        "'{0}' is generic; write Matches by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);
}
