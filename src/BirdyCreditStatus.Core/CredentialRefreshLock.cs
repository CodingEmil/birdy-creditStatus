using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace BirdyCreditStatus.Core;

/// <summary>In-process rotation coordination, not a lock against external CLI writes.
/// File-backed stores share a gate across instances and normalized path spellings;
/// other stores coordinate by object identity. Never hold the gate for usage requests.</summary>
internal static class CredentialRefreshLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Files = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConditionalWeakTable<object, SemaphoreSlim> Stores = new();

    public static Task<IDisposable> AcquireFileAsync(string path, CancellationToken cancellationToken) =>
        AcquireAsync(Files.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1)), cancellationToken);

    public static Task<IDisposable> AcquireStoreAsync(object store, CancellationToken cancellationToken) =>
        AcquireAsync(Stores.GetValue(store, _ => new SemaphoreSlim(1, 1)), cancellationToken);

    private static async Task<IDisposable> AcquireAsync(SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;
        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }
}
