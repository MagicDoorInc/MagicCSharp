using Medallion.Threading;

namespace MagicCSharp.Testing;

/// <summary>
///     Handed out for a re-entrant acquisition. Disposing it releases nothing, because the outer acquisition
///     still owns the lock.
/// </summary>
public sealed class ReentrantHandle : IDistributedSynchronizationHandle
{
    public CancellationToken HandleLostToken => CancellationToken.None;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
