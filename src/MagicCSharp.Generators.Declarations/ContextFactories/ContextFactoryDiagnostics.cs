using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.ContextFactories;

/// <summary>
///     The design-time context factory's diagnostics, <c>MCS1602</c>–<c>MCS1608</c>. Each names the context whose
///     factory is not written and what stands in the way.
/// </summary>
public static class ContextFactoryDiagnostics
{
    public static readonly DiagnosticDescriptor BlankDatabaseName = new DiagnosticDescriptor("MCS1602",
        "[MigrationsDatabase] needs a database name",
        "'{0}' has [MigrationsDatabase] with a blank name; name the database its migrations target",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor FactoryBaseNotFound = new DiagnosticDescriptor("MCS1603",
        "The design-time factory base type is not in the compilation",
        "The design-time factory of '{0}' is not generated: base type '{1}' is not in the compilation; reference the package that has it, or set MagicCSharpDesignTimeContextFactoryBase",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor FactoryBaseUnusable = new DiagnosticDescriptor("MCS1604",
        "The design-time factory base type cannot be derived from",
        "The design-time factory of '{0}' is not generated: base type '{1}' {2}; name a generic class with one type parameter and a constructor taking the database name first",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ContextFailsFactoryBaseConstraint = new DiagnosticDescriptor("MCS1605",
        "The context does not meet the factory base's constraint",
        "The design-time factory of '{0}' is not generated: '{1}' requires its context to be a '{2}'",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor NestedContext = new DiagnosticDescriptor("MCS1606",
        "A nested context gets no generated design-time factory",
        "'{0}' is nested in another type; move it to the top level of its file, or write its IDesignTimeDbContextFactory by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericContext = new DiagnosticDescriptor("MCS1607",
        "A generic context gets no generated design-time factory",
        "'{0}' is generic; write its IDesignTimeDbContextFactory by hand",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor AbstractContext = new DiagnosticDescriptor("MCS1608",
        "An abstract context gets no design-time factory",
        "'{0}' is abstract, and dotnet ef cannot build it; put [MigrationsDatabase] on the context it derives into",
        GeneratorDiagnostics.Category, DiagnosticSeverity.Error, true);
}
