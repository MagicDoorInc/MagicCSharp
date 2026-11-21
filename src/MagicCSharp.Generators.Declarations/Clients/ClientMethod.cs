namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>A client interface method with a client route attribute, as the client class implements it.</summary>
internal sealed record ClientMethod
{
    public required MethodDeclaration Method { get; init; }

    /// <summary><c>Get</c>, <c>Post</c>, <c>Put</c>, <c>Patch</c> or <c>Delete</c>: the <c>HttpMethod</c> property to send with.</summary>
    public required string HttpMethodProperty { get; init; }

    /// <summary>The client's route and the method's template, trimmed of slashes.</summary>
    public required string Path { get; init; }

    public required EquatableArray<ClientParameter> Parameters { get; init; }

    /// <summary>The <c>T</c> of <c>Task&lt;T&gt;</c>, written as C#; null for <c>Task</c>.</summary>
    public required string? ResultFullyQualifiedName { get; init; }

    /// <summary>The result is <c>T?</c>, so a 404 is null.</summary>
    public required bool IsNullableResult { get; init; }
}
