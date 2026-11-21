namespace MagicCSharp.Generators.Declarations.Clients;

/// <summary>The members of a client base that generated client methods call, named as diagnostics show them.</summary>
internal static class ClientBaseMembers
{
    public const string SendRequestWithResponse = "SendRequestAsync<TResponse>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)";

    public const string SendRequest = "SendRequestAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)";

    public const string WithQuery = "WithQuery(string path, params object?[] values)";

    public const string IsNotFound = "IsNotFound(Exception exception)";
}
