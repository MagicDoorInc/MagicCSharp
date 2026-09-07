using System.Collections.Concurrent;
using Medallion.Threading;

namespace MagicCSharp.Testing;

/// <summary>
///     An <see cref="IDistributedLockProvider" /> backed by in-process semaphores, so a test gets real mutual
///     exclusion without a database, ZooKeeper or a file system.
///     <para>
///         It differs from a production provider in one way, and only inside
///         <see cref="SyncEventDispatcher" />: while that dispatcher is running handlers inline, an attempt to
///         acquire a lock some flow already holds succeeds with a no-op handle instead of waiting.
///     </para>
///     <para>
///         That is not a shortcut, it restores production behaviour. In production a handler runs after the
///         emitting use case has released its lock, because dispatch goes through a queue. Running handlers
///         inline puts the handler inside the emitter's lock, where a non-reentrant semaphore would deadlock on
///         something that works in production. The window is scoped to that one dispatch through an
///         <see cref="AsyncLocal{T}" />, so parallel flows in other tests still get ordinary mutex semantics.
///     </para>
/// </summary>
public class InMemoryDistributedLockProvider : IDistributedLockProvider
{
    private readonly ConcurrentDictionary<string, byte> held = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> semaphores = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

    /// <summary>
    ///     Non-zero while <see cref="SyncEventDispatcher" /> is running handlers inline. Held in an
    ///     <see cref="AsyncLocal{T}" /> so it follows that dispatch's continuations and leaks into nothing else.
    /// </summary>
    internal static AsyncLocal<int> ReentrancyDepth { get; } = new AsyncLocal<int>();

    public IDistributedLock CreateLock(string name)
    {
        return new InMemoryDistributedLock(name, this);
    }

    internal SemaphoreSlim GetOrCreateSemaphore(string name)
    {
        return semaphores.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
    }

    internal bool IsHeld(string name)
    {
        return held.ContainsKey(name);
    }

    internal void MarkHeld(string name)
    {
        held[name] = 0;
    }

    internal void MarkReleased(string name)
    {
        held.TryRemove(name, out _);
    }
}

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

/// <summary>Releases the semaphore once, however many times it is disposed.</summary>
public class InMemoryDistributedSynchronizationHandle(
    SemaphoreSlim semaphore,
    InMemoryDistributedLockProvider provider,
    string name) : IDistributedSynchronizationHandle
{
    private bool disposed;

    public CancellationToken HandleLostToken => CancellationToken.None;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        provider.MarkReleased(name);
        semaphore.Release();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

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

/// <summary>
///     A lock provider that grants every acquisition immediately and records the names, for asserting that code
///     locked around the right thing rather than that it serialized correctly.
/// </summary>
public class TrackingDistributedLockProvider(Action<string>? onAcquire = null) : IDistributedLockProvider
{
    private readonly HashSet<string> heldNames = [];

    /// <summary>Every lock name acquired, in order, including repeats.</summary>
    public List<string> AcquiredLockNames { get; } = [];

    public IDistributedLock CreateLock(string name)
    {
        return new TrackingDistributedLock(name, this, onAcquire);
    }

    /// <summary>Whether this name is held right now.</summary>
    public bool IsLockHeld(string name)
    {
        return heldNames.Contains(name);
    }

    private void Acquire(string name)
    {
        AcquiredLockNames.Add(name);
        heldNames.Add(name);
    }

    private void Release(string name)
    {
        heldNames.Remove(name);
    }

    private class TrackingDistributedLock(
        string name,
        TrackingDistributedLockProvider provider,
        Action<string>? onAcquire) : IDistributedLock
    {
        public string Name { get; } = name;

        public IDistributedSynchronizationHandle Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return Grant();
        }

        public ValueTask<IDistributedSynchronizationHandle> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Grant());
        }

        public IDistributedSynchronizationHandle? TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            return Grant();
        }

        public ValueTask<IDistributedSynchronizationHandle?> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IDistributedSynchronizationHandle?>(Grant());
        }

        private IDistributedSynchronizationHandle Grant()
        {
            provider.Acquire(Name);
            onAcquire?.Invoke(Name);
            return new TrackingHandle(Name, provider);
        }
    }

    private class TrackingHandle(string name, TrackingDistributedLockProvider provider) : IDistributedSynchronizationHandle
    {
        public CancellationToken HandleLostToken => CancellationToken.None;

        public void Dispose()
        {
            provider.Release(name);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
