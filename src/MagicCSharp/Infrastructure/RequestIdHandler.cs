namespace MagicCSharp.Infrastructure;

/// <summary>
/// Default implementation of IRequestIdHandler using AsyncLocal for async context tracking.
/// </summary>
public class RequestIdHandler : IRequestIdHandler
{
    private static readonly AsyncLocal<string?> CurrentRequestId = new();

    public string GetCurrentRequestId()
    {
        return CurrentRequestId.Value ?? string.Empty;
    }

    public IDisposable SetRequestId(string requestId)
    {
        var previous = CurrentRequestId.Value;
        CurrentRequestId.Value = requestId;
        return new RequestIdScope(previous);
    }

    public IDisposable SetRequestId()
    {
        return SetRequestId(Guid.NewGuid().ToString());
    }

    public IDisposable SetChildRequestId()
    {
        var currentId = GetCurrentRequestId();
        var childId = string.IsNullOrEmpty(currentId)
            ? Guid.NewGuid().ToString()
            : $"{currentId}-{Guid.NewGuid().ToString()[..8]}";
        return SetRequestId(childId);
    }

    private class RequestIdScope(string? previousRequestId) : IDisposable
    {
        private bool disposed = false;

        public void Dispose()
        {
            if (!disposed)
            {
                CurrentRequestId.Value = previousRequestId;
                disposed = true;
            }
        }
    }
}
