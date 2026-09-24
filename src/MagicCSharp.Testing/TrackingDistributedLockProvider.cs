using Medallion.Threading;

namespace MagicCSharp.Testing;

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
