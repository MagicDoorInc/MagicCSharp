using System.Globalization;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Tests;

/// <summary>
///     Serves a referenced assembly's XML documentation file, as the compiler does for a reference with a .xml next
///     to it. Roslyn's own provider lives in the Workspaces package, which these tests do not otherwise need.
/// </summary>
public sealed class XmlFileDocumentationProvider(byte[] documentationFile) : DocumentationProvider
{
    private readonly Dictionary<string, string> membersById = XDocument.Load(new MemoryStream(documentationFile))
        .Descendants("member")
        .ToDictionary(member => (string)member.Attribute("name")!, member => member.ToString());

    protected override string? GetDocumentationForSymbol(string documentationMemberID, CultureInfo preferredCulture, CancellationToken cancellationToken)
    {
        return membersById.GetValueOrDefault(documentationMemberID);
    }

    public override bool Equals(object? other)
    {
        return ReferenceEquals(this, other);
    }

    public override int GetHashCode()
    {
        return membersById.GetHashCode();
    }
}
