using MagicCSharp.Generators.Declarations;
using MagicCSharp.Generators.Declarations.ContextFactories;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MagicCSharp.Generators;

/// <summary>
///     Writes <c>{Context}Factory</c> for every context marked <c>[GenerateDesignTimeFactory]</c>, so <c>dotnet ef</c>
///     can build it without the host, against the database its <c>[MigrationsDatabase("…")]</c> names. The attribute is
///     the opt-in: <c>[MigrationsDatabase]</c> alone writes nothing. The factory derives from
///     <c>MagicCSharpDesignTimeContextFactoryBase</c> (MagicCSharp's Postgres factory unless the build names another). A
///     project that writes its own factory for the context keeps it, e.g. to add a provider plugin.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DesignTimeContextFactoryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var contexts = context.SyntaxProvider.ForAttributeWithMetadataName(ContextFactoryReader.GenerateDesignTimeFactoryAttributeMetadataName,
            (node, _) => node is ClassDeclarationSyntax,
            (attributeContext, _) => ContextFactoryReader.ReadContext((INamedTypeSymbol)attributeContext.TargetSymbol));

        var contextsWithHandWrittenFactories = context.SyntaxProvider.CreateSyntaxProvider(IsClassWithBaseList, ReadFactoryContext)
            .Where(factoryContext => factoryContext != null)
            .Select((factoryContext, _) => factoryContext!)
            .Collect()
            .Select((factoryContexts, _) => new EquatableArray<string>(factoryContexts.Distinct().OrderBy(name => name, StringComparer.Ordinal)));

        var factoryBase = context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider)
            .Select((compilationAndOptions, _) => ContextFactoryReader.ReadBase(compilationAndOptions.Left, ContextFactoryBaseSetting.Read(compilationAndOptions.Right)));

        context.RegisterSourceOutput(contexts.Combine(contextsWithHandWrittenFactories).Combine(factoryBase), WriteFactory);
    }

    private static bool IsClassWithBaseList(SyntaxNode node, CancellationToken cancellationToken)
    {
        var classDeclaration = node as ClassDeclarationSyntax;
        return classDeclaration != null && classDeclaration.BaseList != null;
    }

    private static string? ReadFactoryContext(GeneratorSyntaxContext syntaxContext, CancellationToken cancellationToken)
    {
        var type = syntaxContext.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)syntaxContext.Node, cancellationToken) as INamedTypeSymbol;
        return type == null ? null : ContextFactoryReader.ReadFactoryContext(type);
    }

    private static void WriteFactory(SourceProductionContext sourceProductionContext,
        ((MigrationsDatabaseContext Context, EquatableArray<string> ContextsWithHandWrittenFactories) Declarations, ContextFactoryBase FactoryBase) input)
    {
        var migrationsDatabaseContext = input.Declarations.Context;

        var declarationRefusal = ContextFactoryPlanner.DeclarationRefusal(migrationsDatabaseContext);
        if (declarationRefusal != null)
        {
            sourceProductionContext.ReportDiagnostic(declarationRefusal.ToDiagnostic());
            return;
        }

        if (ContextFactoryPlanner.HasHandWrittenFactory(migrationsDatabaseContext, input.Declarations.ContextsWithHandWrittenFactories))
        {
            return;
        }

        var refusals = ContextFactoryPlanner.Refusals(migrationsDatabaseContext, input.FactoryBase);
        foreach (var refusalDiagnosticInfo in refusals)
        {
            sourceProductionContext.ReportDiagnostic(refusalDiagnosticInfo.ToDiagnostic());
        }

        if (refusals.Count > 0)
        {
            return;
        }

        var generatedFile = ContextFactoryWriter.WriteFile(migrationsDatabaseContext, input.FactoryBase);
        sourceProductionContext.AddSource(generatedFile.HintName, generatedFile.Text);
    }
}
