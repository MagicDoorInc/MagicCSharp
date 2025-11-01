namespace MagicCSharp.Infrastructure;

/// <summary>
/// Interface for managing request IDs across async contexts.
/// </summary>
public interface IRequestIdHandler
{
    /// <summary>
    /// Get the current request ID.
    /// </summary>
    string GetCurrentRequestId();

    /// <summary>
    /// Set the request ID to a specific value.
    /// </summary>
    IDisposable SetRequestId(string requestId);

    /// <summary>
    /// Set a new request ID.
    /// </summary>
    IDisposable SetRequestId();

    /// <summary>
    /// Set a new child request ID based on the current request ID.
    /// </summary>
    IDisposable SetChildRequestId();
}
