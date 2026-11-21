namespace MagicCSharp.Generators.Declarations;

/// <summary>A logged parameter of a <c>[LogExecuting]</c> class's method that looks like a secret.</summary>
public sealed record SecretParameterReading
{
    /// <summary>Where the method is declared: the key that ties the parameter to the method a decorator would log.</summary>
    public required SourceLocation? MethodLocation { get; init; }

    public required string ParameterName { get; init; }
}
