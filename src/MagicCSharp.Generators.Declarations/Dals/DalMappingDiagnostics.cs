using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>
///     The Dal mapping diagnostics, <c>MCS12xx</c>, each on a <c>[GenerateDalMapping]</c> Dal; the message says what
///     stands in the way. <c>MCS1201</c> and <c>MCS1202</c> fire only when the Dal lacks that method, so it is
///     incomplete either way. <c>MCS1203</c> fires only when the Dal opted into <c>From</c> (it lists <c>IDalFromEdit</c>, or writes neither
///     <c>ToEntity</c> nor <c>Apply</c>) and lacks one; a Dal that maps by hand without <c>IDalFromEdit</c> never gets it.
/// </summary>
public static class DalMappingDiagnostics
{
    public static readonly DiagnosticDescriptor DalToEntityNotGenerated = new DiagnosticDescriptor("MCS1201",
        "ToEntity cannot be generated for this Dal",
        "'{0}.ToEntity' is not generated: '{1}.{2}' ({3}) {4}; give the Dal a public property '{2}' of type {3}, or write ToEntity by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor DalApplyNotGenerated = new DiagnosticDescriptor("MCS1202",
        "Apply cannot be generated for this Dal",
        "'{0}.Apply' is not generated: '{1}.{2}' ({3}) {4}; give the Dal a public settable property '{2}' of type {3}, or write Apply by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor DalFromNotGenerated = new DiagnosticDescriptor("MCS1203",
        "From cannot be generated for this Dal",
        "'{0}.From' is not generated: '{0}.{1}' ({2}) is set only when the row is created, and {3}; add '{1}' to '{4}', or write From by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NestedDal = new DiagnosticDescriptor("MCS1204",
        "A nested Dal gets no generated mapping",
        "'{0}' is nested in another type; move it to the top level of its file, or write ToEntity, Apply and From by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericDal = new DiagnosticDescriptor("MCS1205",
        "A generic Dal gets no generated mapping",
        "'{0}' is generic; write ToEntity, Apply and From by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor EntityWithoutParameterlessConstructor = new DiagnosticDescriptor("MCS1206",
        "ToEntity cannot build an entity without a parameterless constructor",
        "'{0}.ToEntity' is not generated: '{1}' has no accessible parameterless constructor to build it with; write ToEntity by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor DalNotPartial = new DiagnosticDescriptor("MCS1207",
        "A [GenerateDalMapping] Dal must be partial",
        "'{0}' has [GenerateDalMapping] but is not partial; add 'partial' so the generated ToEntity, Apply and From can be attached to it",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NotADal = new DiagnosticDescriptor("MCS1208",
        "[GenerateDalMapping] is on a class that is not a Dal",
        "'{0}' has [GenerateDalMapping] but is not a concrete class implementing IDalTransform<TEntity, TEdit>; put the attribute on the Dal, or remove it",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);
}
