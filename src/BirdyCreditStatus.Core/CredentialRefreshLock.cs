using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace BirdyCreditStatus.Core;

/// <summary>In-process rotation coordination, not a lock against external CLI writes.
/// File-backed stores share a gate across instances and normalized path spellings;
/// other stores coordinate by object identity. Never hold the gate for usage requests.</summary>
internal static class CredentialRefreshLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Files = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Keys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConditionalWeakTable<object, SemaphoreSlim> Stores = new();

    public static Task<IDisposable> AcquireFileAsync(string path, CancellationToken cancellationToken) =>
        AcquireAsync(Files.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1)), cancellationToken);

    /// <summary>Gate für Pi-Sections (F011-T3): ein Lock pro Datei+Section, getrennt von
    /// den Datei-Gates (Pi speichert nie, serialisiert aber parallele Rotation).
    /// Wirft nie (unbrauchbarer Pfad fällt auf den Rohtext zurück).</summary>
    public static Task<IDisposable> AcquirePiSectionAsync(string? path, string? section, CancellationToken cancellationToken) =>
        AcquireAsync(Keys.GetOrAdd(PiSectionKey(path, section), _ => new SemaphoreSlim(1, 1)), cancellationToken);

    private static string PiSectionKey(string? path, string? section)
    {
        string file;
        try
        {
            file = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            file = path ?? string.Empty;
        }

        return "pi:" + file + "\0" + section;
    }

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
