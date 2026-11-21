namespace MagicCSharp.Generators.Declarations.AssemblyLoading;

/// <summary>Writes <c>{RootNamespace}.AssemblyLoader</c>, whose module initializer loads every referenced project.</summary>
public static class AssemblyLoaderWriter
{
    public static GeneratedFile WriteFile(AssemblyLoaderDeclaration declaration)
    {
        var sourceWriter = new SourceWriter();
        sourceWriter.WriteFileHeader();

        var hasNamespace = declaration.Namespace.Length > 0;
        if (hasNamespace)
        {
            sourceWriter.WriteLine("namespace " + declaration.Namespace);
            sourceWriter.OpenBlock();
        }

        sourceWriter.WriteLine("/// <summary>");
        sourceWriter.WriteLine("///     Loads every project this one references before Main runs, so a scan for use cases and handlers also");
        sourceWriter.WriteLine("///     finds the projects nothing has touched yet. Written from the project references at each build.");
        sourceWriter.WriteLine("/// </summary>");
        sourceWriter.WriteLine("public static class " + AssemblyLoaderReader.LoaderClassName);
        sourceWriter.OpenBlock();
        sourceWriter.WriteLine("[" + GlobalAlias.Prefix + "System.Runtime.CompilerServices.ModuleInitializer]");
        sourceWriter.WriteLine("public static void Load()");
        sourceWriter.OpenBlock();
        foreach (var loadedAssembly in declaration.Assemblies)
        {
            sourceWriter.WriteLine(LoadStatement(loadedAssembly));
        }

        sourceWriter.CloseBlock();
        sourceWriter.CloseBlock();

        if (hasNamespace)
        {
            sourceWriter.CloseBlock();
        }

        var hintPrefix = hasNamespace ? declaration.Namespace + "." : "";
        return new GeneratedFile { HintName = hintPrefix + AssemblyLoaderReader.LoaderClassName + ".g.cs", Text = sourceWriter.ToString() };
    }

    private static string LoadStatement(LoadedAssembly loadedAssembly)
    {
        if (loadedAssembly.PublicTypeOperand != null)
        {
            return "_ = typeof(" + loadedAssembly.PublicTypeOperand + ").Assembly;";
        }

        return "_ = " + GlobalAlias.Prefix + "System.Reflection.Assembly.Load(new " + GlobalAlias.Prefix + "System.Reflection.AssemblyName(\"" +
               loadedAssembly.AssemblyName + "\"));";
    }
}
