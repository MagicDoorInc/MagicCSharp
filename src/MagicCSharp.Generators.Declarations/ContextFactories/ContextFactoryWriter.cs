using Microsoft.CodeAnalysis.CSharp;

namespace MagicCSharp.Generators.Declarations.ContextFactories;

/// <summary>Writes <c>{Context}Factory</c>, the design-time factory <c>dotnet ef</c> finds by type.</summary>
public static class ContextFactoryWriter
{
    public static GeneratedFile WriteFile(MigrationsDatabaseContext context, ContextFactoryBase factoryBase)
    {
        var factoryName = context.Name + "Factory";
        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();

        var hasNamespace = context.Namespace.Length > 0;
        if (hasNamespace)
        {
            sourceWriter.WriteLine("namespace " + context.Namespace);
            sourceWriter.OpenBlock();
        }

        sourceWriter.WriteLine("/// <summary>");
        sourceWriter.WriteLine("///     Lets dotnet ef build " + context.Name + " outside the running application, against the database its");
        sourceWriter.WriteLine("///     [MigrationsDatabase] names. Written at each build.");
        sourceWriter.WriteLine("/// </summary>");
        sourceWriter.WriteLine(context.Accessibility + " class " + factoryName + " : " + factoryBase.FullyQualifiedNameWithoutTypeArguments + "<" +
                         context.FullyQualifiedName + ">");
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine("public " + factoryName + "()");
        sourceWriter.WriteLine("    : base(" + SymbolDisplay.FormatLiteral(context.DatabaseName ?? "", true) + ")");
        sourceWriter.OpenBlock();
        sourceWriter.CloseBlock();
        sourceWriter.CloseBlock();

        if (hasNamespace)
        {
            sourceWriter.CloseBlock();
        }

        var hintPrefix = hasNamespace ? context.Namespace + "." : "";
        return new GeneratedFile { HintName = hintPrefix + factoryName + ".g.cs", Text = sourceWriter.ToString() };
    }
}
