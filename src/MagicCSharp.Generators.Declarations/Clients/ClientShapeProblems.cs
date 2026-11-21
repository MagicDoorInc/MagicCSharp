namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>What is wrong with a <c>[MagicClient]</c> interface itself, before any of its methods are read.</summary>
internal static class ClientShapeProblems
{
    public static List<DiagnosticInfo> Find(TypeDeclaration interfaceDeclaration, ClientConvention convention)
    {
        var shapeProblems = new List<DiagnosticInfo>();
        if (convention.DeclaringAssemblies.Count > 1)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.MoreThanOneClientConvention, interfaceDeclaration.Location, interfaceDeclaration.Name,
                convention.DeclaringAssemblies[0], convention.DeclaringAssemblies[1]));
        }

        if (interfaceDeclaration.TypeParameters.Count > 0)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.GenericClientInterface, interfaceDeclaration.Location, interfaceDeclaration.Name));
        }

        if (interfaceDeclaration.IsNested)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.NestedClientInterface, interfaceDeclaration.Location, interfaceDeclaration.Name));
        }

        var name = interfaceDeclaration.Name;
        var hasClientName = name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]);
        if (!hasClientName)
        {
            shapeProblems.Add(ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.ClientInterfaceNameWithoutI, interfaceDeclaration.Location, name));
        }

        return shapeProblems;
    }
}
