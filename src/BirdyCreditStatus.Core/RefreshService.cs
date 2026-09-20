namespace BirdyCreditStatus.Core;

/// <summary>Manueller Abruf (kein Timer, F004): Adapter → Cache. Nur Erfolge überschreiben
/// den jeweiligen Cache-Eintrag; Fehler/Setup einer Karte blockieren die andere nicht.
/// Schlüssel = Kontoname (F006-T2, generische Naht: ein Adapter je Konto).</summary>
public sealed class RefreshService
{
    private readonly Dictionary<string, IQuotaAdapter> _adapters;
    private readonly SnapshotCache _cache;

    /// <summary>Alt-Pfad (F001): ein Adapter, Ergebnis über <see cref="RefreshAsync"/>.</summary>
    public RefreshService(IQuotaAdapter adapter, SnapshotCache cache)
        : this(new Dictionary<string, IQuotaAdapter> { ["Claude"] = adapter }, cache)
    {
    }

    /// <summary>Multi-Pfad (F002): ein Adapter je Provider, Schlüssel = Provider-Name.</summary>
    public RefreshService(IReadOnlyDictionary<string, IQuotaAdapter> adapters, SnapshotCache cache)
    {
        _adapters = new Dictionary<string, IQuotaAdapter>(adapters, StringComparer.Ordinal);
        _cache = cache;
    }

    /// <summary>Registriert einen Konto-Adapter zur Laufzeit bzw. ersetzt ihn
    /// (F006-T3: Hinzufügen/Umbenennen ohne Neustart). Leere Schlüssel werden ignoriert.</summary>
    public void UpsertAdapter(string key, IQuotaAdapter adapter)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _adapters[key] = adapter;
    }

    /// <summary>Entfernt einen Konto-Adapter (F006-T3: Entfernen ohne Neustart).
    /// Gibt false zurück, wenn der Schlüssel nicht registriert war.</summary>
    public bool RemoveAdapter(string key) => !string.IsNullOrWhiteSpace(key) && _adapters.Remove(key);

    /// <summary>Alt-Pfad (F001): ruft den einzigen Adapter ab.</summary>
    public async Task<QuotaResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var all = await RefreshAllAsync(cancellationToken);
        return all.Values.Single();
    }

    /// <summary>Ruft jeden Adapter parallel ab (D010: nur bei Öffnen + Refresh, kein Timer),
    /// Schlüssel = Kontoname. Nur Erfolge überschreiben den jeweiligen Cache-Eintrag.</summary>
    public async Task<IReadOnlyDictionary<string, QuotaResult>> RefreshAllAsync(
        CancellationToken cancellationToken = default)
    {
        var fetches = _adapters.Select(async kvp =>
        {
            QuotaResult result;
            try
            {
                result = await kvp.Value.FetchAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = new QuotaResult(false, null, "n/a – Key prüfen / offline");
            }

            return (Key: kvp.Key, Result: result);
        }).ToList();

        var fetched = await Task.WhenAll(fetches);
        var results = new Dictionary<string, QuotaResult>(StringComparer.Ordinal);
        foreach (var (key, result) in fetched)
        {
            if (result.IsSuccess && result.Snapshot is not null)
            {
                _cache.Save(result.Snapshot);
            }

            results[key] = result;
        }

        return results;
    }
}
