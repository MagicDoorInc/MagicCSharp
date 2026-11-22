namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Which parameters an <c>Executing:</c> line names: all of them except a cancellation token, which says nothing
///     about the call, and one marked <c>[NotLogged]</c>.
/// </summary>
public static class LoggedParameters
{
    public const string NotLoggedAttributeFullyQualifiedName = "global::MagicCSharp.UseCases.NotLoggedAttribute";

    private const string CancellationTokenFullyQualifiedName = "global::System.Threading.CancellationToken";

    public static bool IsLogged(ParameterDeclaration parameter)
    {
        if (parameter.Type.FullyQualifiedName.TrimEnd('?') == CancellationTokenFullyQualifiedName)
        {
            return false;
        }

        return !IsMarkedNotLogged(parameter);
    }

    public static bool IsMarkedNotLogged(ParameterDeclaration parameter)
    {
        return parameter.Attributes.Any(attribute => attribute.Type.FullyQualifiedName == NotLoggedAttributeFullyQualifiedName);
    }

    public static EquatableArray<ParameterDeclaration> Choose(MethodDeclaration method)
    {
        return new EquatableArray<ParameterDeclaration>(method.Parameters.Where(IsLogged));
    }
}
