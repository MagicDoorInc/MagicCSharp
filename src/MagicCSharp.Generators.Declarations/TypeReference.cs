namespace MagicCSharp.Generators.Declarations;

/// <summary>A type as a declaration uses it: a parameter's, a return value's, a property's, a base type.</summary>
public sealed record TypeReference
{
    /// <summary>The simple name: <c>Lease</c>, <c>Task</c>, <c>Lease[]</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The containing namespace, empty for the global namespace.</summary>
    public required string Namespace { get; init; }

    /// <summary>
    ///     The name to write in generated code: <c>global::</c>-qualified, keywords for special types (<c>long</c>,
    ///     <c>string</c>), and <c>?</c> where the type is nullable.
    /// </summary>
    public required string FullyQualifiedName { get; init; }

    public required TypeReferenceKind Kind { get; init; }

    public required bool IsNullable { get; init; }

    public required bool IsValueType { get; init; }

    /// <summary>
    ///     The compiler could not resolve the type: missing, or written by another generator, whose output this
    ///     compilation does not include.
    /// </summary>
    public required bool IsUnresolved { get; init; }

    public required EquatableArray<TypeReference> TypeArguments { get; init; }

    /// <summary>The element type of an array; null for anything else.</summary>
    public required TypeReference? ElementType { get; init; }
}
