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
