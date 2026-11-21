namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     Generated file names. The namespace is part of the name, so two same-named classes in one project get two
///     files instead of a generator crash.
/// </summary>
public static class HintNames
{
    /// <summary><c>{Namespace}.{Name}.{purpose}.g.cs</c>, e.g. <c>Acme.Leasing.SignLeaseUseCase.Interface.g.cs</c>.</summary>
    public static string For(TypeDeclaration type, string purpose)
    {
        var namespacePrefix = type.Namespace == "" ? "" : type.Namespace + ".";
        return namespacePrefix + type.Name + "." + purpose + ".g.cs";
    }
}
