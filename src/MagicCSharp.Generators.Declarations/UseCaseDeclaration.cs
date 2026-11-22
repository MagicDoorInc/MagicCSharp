namespace MagicCSharp.Generators.Declarations;

/// <summary>A <c>[GenerateInterface]</c> use case class and the interface it gets.</summary>
public sealed record UseCaseDeclaration
{
    /// <summary>The name of the method that runs a use case, and the one <c>[LogExecuting]</c> writes its line on.</summary>
    public const string ExecuteMethodName = "Execute";

    public required TypeDeclaration Class { get; init; }

    /// <summary><c>I</c> and the class name: <c>ISignLeaseUseCase</c>.</summary>
    public required string InterfaceName { get; init; }

    /// <summary><c>global::</c>-qualified, in the class's namespace.</summary>
    public required string InterfaceFullyQualifiedName { get; init; }

    /// <summary>The use case marker interface the generated interface extends.</summary>
    public required TypeReference MarkerInterface { get; init; }

    /// <summary>
    ///     The class's own public instance methods, in declaration order: the interface's members. A method that
    ///     overrides one, implements another interface's member (<c>Dispose</c>) or is written by the compiler is not
    ///     among them.
    /// </summary>
    public required EquatableArray<MethodDeclaration> Methods { get; init; }

    /// <summary>The <c>Execute</c> methods among <see cref="Methods" />: what running the use case calls.</summary>
    public EquatableArray<MethodDeclaration> ExecuteMethods
    {
        get
        {
            return new EquatableArray<MethodDeclaration>(Methods.Where(method => method.Name == ExecuteMethodName));
        }
    }
}
