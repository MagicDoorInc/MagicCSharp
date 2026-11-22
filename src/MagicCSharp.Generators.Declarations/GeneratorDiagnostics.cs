using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     The diagnostics MagicCSharp's generators report. Ids are <c>MCS1000</c>–<c>MCS1999</c>, one hundred per
///     generator area: <c>MCS10xx</c> declarations and <c>[GenerateInterface]</c>, <c>MCS11xx</c> the Executing
///     wrapper, <c>MCS12xx</c> Dal mapping and registration, <c>MCS13xx</c> filters, <c>MCS14xx</c> test services,
///     <c>MCS15xx</c> clients, <c>MCS16xx</c> app start-up. Every one is an error: a generator that cannot write
///     what the declaration asks for says so instead of writing something else.
/// </summary>
public static class GeneratorDiagnostics
{
    public const string Category = "MagicCSharp.Generators";

    public static readonly DiagnosticDescriptor NotPartial = new DiagnosticDescriptor("MCS1001",
        "A [GenerateInterface] class must be partial",
        "'{0}' has [GenerateInterface] but is not partial; add 'partial' so the generated interface can be attached to it",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NoPublicExecute = new DiagnosticDescriptor("MCS1002",
        "A [GenerateInterface] class needs a public Execute method",
        "'{0}' has [GenerateInterface] but declares no public instance Execute method for its interface",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NestedClass = new DiagnosticDescriptor("MCS1003",
        "A [GenerateInterface] class cannot be nested",
        "'{0}' is nested in another type; move it to the top level of its file, or write its interface by hand",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericClass = new DiagnosticDescriptor("MCS1004",
        "A [GenerateInterface] class cannot be generic",
        "'{0}' is generic; write its interface by hand",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor StaticClass = new DiagnosticDescriptor("MCS1005",
        "A [GenerateInterface] class cannot be static",
        "'{0}' is static, and a static class cannot implement an interface",
        Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor MarkerInterfaceNotFound = new DiagnosticDescriptor("MCS1007",
        "The use case marker interface was not found",
        "'{0}' needs the use case marker interface '{1}', which is not an interface this project can see; reference the package that declares it, or set the MSBuild property MagicCSharpUseCaseMarkerInterface to your own",
        Category, DiagnosticSeverity.Error, true);
}
