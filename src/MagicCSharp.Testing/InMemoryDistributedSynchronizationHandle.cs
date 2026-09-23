using Medallion.Threading;

namespace MagicCSharp.Testing;

/// <summary>Releases the semaphore once, however many times it is isDisposed.</summary>
public class InMemoryDistributedSynchronizationHandle(
    SemaphoreSlim semaphore,
    InMemoryDistributedLockProvider provider,
    string name) : IDistributedSynchronizationHandle
{
    private bool isDisposed;

    public CancellationToken HandleLostToken => CancellationToken.None;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        provider.MarkReleased(name);
        semaphore.Release();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
