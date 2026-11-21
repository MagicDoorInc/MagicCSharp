using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>
///     The use cases whose interfaces <c>[GenerateInterface]</c> writes in one compilation. Another generator does
///     not see those interfaces (no generator sees another's output), so it looks the class up here instead.
/// </summary>
public sealed record GeneratedInterfaceIndex
{
    public required EquatableArray<UseCaseDeclaration> Declarations { get; init; }

    /// <summary>
    ///     Finds the use case behind <paramref name="interfaceSymbol" />. An unresolved interface (an error type,
    ///     because it is generated) may carry no namespace, so it matches by name when exactly one use case has that
    ///     interface name.
    /// </summary>
    public bool TryFind(ITypeSymbol interfaceSymbol, [NotNullWhen(true)] out UseCaseDeclaration? declaration)
    {
        return TryFind(DeclarationReader.ReadTypeReference(interfaceSymbol), out declaration);
    }

    /// <summary>
    ///     Finds the use case behind <paramref name="interfaceType" />, as read into a pipeline value (a
    ///     <c>typeof</c> attribute argument, a parameter type), with the same rules as for a symbol.
    /// </summary>
    public bool TryFind(TypeReference interfaceType, [NotNullWhen(true)] out UseCaseDeclaration? declaration)
    {
        if (interfaceType.Namespace != "" && TryFind(interfaceType.Namespace + "." + interfaceType.Name, out declaration))
        {
            return true;
        }

        if (!interfaceType.IsUnresolved)
        {
            declaration = null;
            return false;
        }

        var sameNamed = Declarations.Where(candidate => candidate.InterfaceName == interfaceType.Name).ToArray();
        declaration = sameNamed.Length == 1 ? sameNamed[0] : null;
        return declaration != null;
    }

    /// <summary>Finds a use case by its interface's full name, <c>Acme.Leasing.ISignLeaseUseCase</c>.</summary>
    public bool TryFind(string interfaceFullName, [NotNullWhen(true)] out UseCaseDeclaration? declaration)
    {
        var fullyQualifiedName = "global::" + interfaceFullName;
        declaration = Declarations.FirstOrDefault(candidate => candidate.InterfaceFullyQualifiedName == fullyQualifiedName);
        return declaration != null;
    }
}
