namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>What the client base and registration a convention names lack for the methods a client will have.</summary>
internal static class ClientConventionProblems
{
    public static IEnumerable<DiagnosticInfo> Find(TypeDeclaration interfaceDeclaration, ClientConvention convention, List<ClientMethod> clientMethods)
    {
        var neededMembers = new List<string>();
        if (clientMethods.Any(clientMethod => clientMethod.ResultFullyQualifiedName != null))
        {
            neededMembers.Add(ClientBaseMembers.SendRequestWithResponse);
        }

        if (clientMethods.Any(clientMethod => clientMethod.ResultFullyQualifiedName == null))
        {
            neededMembers.Add(ClientBaseMembers.SendRequest);
        }

        if (clientMethods.Any(clientMethod => clientMethod.Parameters.Any(IsQueryParameter)))
        {
            neededMembers.Add(ClientBaseMembers.WithQuery);
        }

        if (clientMethods.Any(clientMethod => clientMethod.IsNullableResult))
        {
            neededMembers.Add(ClientBaseMembers.IsNotFound);
        }

        foreach (var missingMember in neededMembers.Where(neededMember => !convention.BaseMembers.Contains(neededMember)))
        {
            yield return ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.ClientBaseLacksMember, interfaceDeclaration.Location, interfaceDeclaration.Name,
                convention.BaseFullyQualifiedName, missingMember);
        }

        if (!convention.HasRegistrationMethod)
        {
            yield return ClientGeneratorDiagnostics.Problem(ClientGeneratorDiagnostics.ClientRegistrationNotFound, interfaceDeclaration.Location, interfaceDeclaration.Name,
                convention.RegistrationTypeFullyQualifiedName, convention.RegistrationMethod);
        }
    }

    private static bool IsQueryParameter(ClientParameter clientParameter)
    {
        return clientParameter.Role == ClientParameterRole.QueryValue || clientParameter.Role == ClientParameterRole.QueryObject;
    }
}
