using Microsoft.CodeAnalysis;

namespace MagicCSharp.Generators.Declarations;

/// <summary>How the readers print a symbol's name into the declaration model.</summary>
internal static class SymbolDisplayFormats
{
    /// <summary><c>global::</c>-qualified, with <c>?</c> on nullable reference types.</summary>
    public static readonly SymbolDisplayFormat FullyQualifiedWithNullableAnnotation =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary><c>global::</c>-qualified, as the name of a declaration rather than of a use of its type.</summary>
    public static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat;
}
