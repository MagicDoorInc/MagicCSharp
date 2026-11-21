using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>Names of the types and attributes the client readers look for, read the same way by each of them.</summary>
internal static class ClientSymbolNames
{
    public const string Task = "System.Threading.Tasks.Task";

    public const string CancellationToken = "System.Threading.CancellationToken";

    public const string Guid = "System.Guid";

    // Namespace-qualified, without generic arguments: Task and Task<T> both read System.Threading.Tasks.Task.
    private static readonly SymbolDisplayFormat MetadataNameFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None);

    public static string MetadataName(ITypeSymbol type)
    {
        return type.ToDisplayString(MetadataNameFormat);
    }

    public static string AttributeName(AttributeData attribute)
    {
        return attribute.AttributeClass == null ? "" : MetadataName(attribute.AttributeClass);
    }
}
