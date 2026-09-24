using Medallion.Threading;

namespace MagicCSharp.Testing;

/// <inheritdoc cref="InMemoryDistributedLockProvider" />
public class InMemoryDistributedLock(string name, InMemoryDistributedLockProvider provider) : IDistributedLock
{
    public string Name { get; } = name;

    private SemaphoreSlim Semaphore => provider.GetOrCreateSemaphore(Name);

    public IDistributedSynchronizationHandle Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (ShouldReenter())
        {
            return new ReentrantHandle();
        }

        var waitTimeout = timeout ?? Timeout.InfiniteTimeSpan;
        if (!Semaphore.Wait(waitTimeout, cancellationToken))
        {
            throw new TimeoutException($"Failed to acquire in-memory distributed lock '{Name}' within {waitTimeout}.");
        }

        provider.MarkHeld(Name);
        return new InMemoryDistributedSynchronizationHandle(Semaphore, provider, Name);
    }

    public async ValueTask<IDistributedSynchronizationHandle> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (ShouldReenter())
        {
            return new ReentrantHandle();
        }

        var waitTimeout = timeout ?? Timeout.InfiniteTimeSpan;
        if (!await Semaphore.WaitAsync(waitTimeout, cancellationToken).ConfigureAwait(false))
        {
            throw new TimeoutException($"Failed to acquire in-memory distributed lock '{Name}' within {waitTimeout}.");
        }

        provider.MarkHeld(Name);
        return new InMemoryDistributedSynchronizationHandle(Semaphore, provider, Name);
    }

    public IDistributedSynchronizationHandle? TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        if (ShouldReenter())
        {
            return new ReentrantHandle();
        }

        if (!Semaphore.Wait(timeout, cancellationToken))
        {
            return null;
        }

        provider.MarkHeld(Name);
        return new InMemoryDistributedSynchronizationHandle(Semaphore, provider, Name);
    }

    public async ValueTask<IDistributedSynchronizationHandle?> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        if (ShouldReenter())
        {
            return new ReentrantHandle();
        }

        if (!await Semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        provider.MarkHeld(Name);
        return new InMemoryDistributedSynchronizationHandle(Semaphore, provider, Name);
    }

    private bool ShouldReenter()
    {
        return InMemoryDistributedLockProvider.ReentrancyDepth.Value > 0 && provider.IsHeld(Name);
    }
}
