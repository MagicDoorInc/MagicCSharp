namespace MagicCSharp.Generators.Declarations;

/// <summary>A <c>[LogExecuting]</c> class, read as much as its decorator and its refusals need.</summary>
public sealed record ExecutingLogClassReading
{
    public required TypeDeclaration UseCaseClass { get; init; }

    /// <summary>Where the class writes its own <c>Executing:</c> line; null when it writes none.</summary>
    public required SourceLocation? OwnExecutingLineLocation { get; init; }

    /// <summary>Every interface the class implements that the compiler resolves; a generated one is not among them.</summary>
    public required EquatableArray<ImplementedInterfaceReading> ImplementedInterfaces { get; init; }

    /// <summary>Logged parameters that look like secrets, across the class's public <c>Execute</c> methods and its implementations of interface <c>Execute</c> methods.</summary>
    public required EquatableArray<SecretParameterReading> SecretParameters { get; init; }
}
