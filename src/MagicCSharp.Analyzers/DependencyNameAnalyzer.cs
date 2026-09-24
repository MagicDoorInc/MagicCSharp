using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0007: an injected dependency on one of the codebase's own interfaces is named after the interface.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DependencyNameAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0007";

    private const string InterfacePrefix = "I";
    private const string DroppedSuffix = "UseCase";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, "Dependency parameter must be named after its type",
        "Dependency of type '{0}' is named '{1}'; name it '{2}'", "Naming", DiagnosticSeverity.Error, true,
        "A shortened dependency name ('updateCategory' for IUpdateMaintenanceRequestCategoryUseCase) forces every " +
        "reader to look up the constructor to learn what the call site does. A constructor or [FromServices] " +
        "parameter whose type is an interface from this codebase or from MagicCSharp is named after the " +
        "interface without the leading 'I'; use cases also drop the 'UseCase' suffix.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.Parameter);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var parameter = (IParameterSymbol)context.Symbol;
        if (!SymbolFacts.IsInjectedDependency(parameter))
        {
            return;
        }

        var type = parameter.Type as INamedTypeSymbol;
        if (type == null || type.TypeKind != TypeKind.Interface || type.IsGenericType)
        {
            return;
        }

        if (!SymbolFacts.IsFirstParty(type, context.Compilation))
        {
            return;
        }

        var expectedName = ExpectedName(type.Name);
        if (expectedName.Length == 0 || parameter.Name == expectedName)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, parameter.Locations[0], type.Name, parameter.Name,
            expectedName));
    }

    private static string ExpectedName(string typeName)
    {
        var bareName = typeName;
        var hasInterfacePrefix = typeName.Length > 1 && typeName.StartsWith(InterfacePrefix) && char.IsUpper(typeName[1]);
        if (hasInterfacePrefix)
        {
            bareName = typeName.Substring(1);
        }

        if (bareName.Length > DroppedSuffix.Length && bareName.EndsWith(DroppedSuffix))
        {
            bareName = bareName.Substring(0, bareName.Length - DroppedSuffix.Length);
        }

        return IdentifierWords.ToCamelCase(bareName);
    }
}
