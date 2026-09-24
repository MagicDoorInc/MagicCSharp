using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MagicCSharp.Analyzers;

/// <summary>MCS0020: the infrastructure dependencies every class takes have one name each.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CanonicalDependencyNameAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCS0020";

    private static readonly KeyValuePair<string, string>[] CanonicalNamesByTypeName =
    {
        new KeyValuePair<string, string>(KnownTypeNames.TimeProvider, "timeProvider"),
        new KeyValuePair<string, string>(KnownTypeNames.KeyGenService, "keyGenService"),
        new KeyValuePair<string, string>(KnownTypeNames.EventDispatcher, "eventDispatcher"),
        new KeyValuePair<string, string>(KnownTypeNames.DistributedLockProvider, "distributedLockProvider"),
        new KeyValuePair<string, string>(KnownTypeNames.Logger, "logger"),
        new KeyValuePair<string, string>(KnownTypeNames.GenericLogger, "logger"),
    };

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId,
        "Infrastructure dependency parameter must use its canonical name", "Dependency of type '{0}' is named '{1}'; name it '{2}'", "Naming",
        DiagnosticSeverity.Error, true,
        "TimeProvider, IKeyGenService, IEventDispatcher, IDistributedLockProvider and ILogger<T> are injected " +
        "everywhere under the names timeProvider, keyGenService, eventDispatcher, distributedLockProvider and logger, whether " +
        "through a constructor or a [FromServices] parameter, so a reader never has to check which one a " + "call site uses.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var canonicalNames = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
            foreach (var pair in CanonicalNamesByTypeName)
            {
                var type = compilationContext.Compilation.GetTypeByMetadataName(pair.Key);
                if (type != null)
                {
                    canonicalNames[type] = pair.Value;
                }
            }

            if (canonicalNames.Count == 0)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(symbolContext => Analyze(symbolContext, canonicalNames), SymbolKind.Parameter);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, Dictionary<INamedTypeSymbol, string> canonicalNames)
    {
        var parameter = (IParameterSymbol)context.Symbol;
        if (!SymbolFacts.IsInjectedDependency(parameter))
        {
            return;
        }

        var type = parameter.Type as INamedTypeSymbol;
        if (type == null)
        {
            return;
        }

        string canonicalName;
        if (!canonicalNames.TryGetValue(type.OriginalDefinition, out canonicalName) || parameter.Name == canonicalName)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, parameter.Locations[0], type.Name, parameter.Name,
            canonicalName));
    }
}
