namespace BirdyCreditStatus.Core;

/// <summary>Manueller Abruf (kein Timer, F004): Adapter → Cache. Nur Erfolge überschreiben
/// den jeweiligen Cache-Eintrag; Fehler/Setup einer Karte blockieren die andere nicht.
/// Schlüssel = Kontoname (F006-T2, generische Naht: ein Adapter je Konto).</summary>
public sealed class RefreshService
{
    private sealed class Registration(IQuotaAdapter adapter)
    {
        public IQuotaAdapter Adapter { get; } = adapter;
        public QuotaResult? Result { get; set; }
        public long Generation { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Registration> _adapters;
    private readonly SnapshotCache _cache;

    /// <summary>Alt-Pfad (F001): ein Adapter, Ergebnis über <see cref="RefreshAsync"/>.</summary>
    public RefreshService(IQuotaAdapter adapter, SnapshotCache cache)
        : this(new Dictionary<string, IQuotaAdapter> { ["Claude"] = adapter }, cache)
    {
    }

    /// <summary>Multi-Pfad (F002): ein Adapter je Provider, Schlüssel = Provider-Name.</summary>
    public RefreshService(IReadOnlyDictionary<string, IQuotaAdapter> adapters, SnapshotCache cache)
    {
        _adapters = adapters.ToDictionary(p => p.Key, p => new Registration(p.Value), StringComparer.Ordinal);
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

        lock (_gate)
        {
            _adapters[key] = new Registration(adapter);
        }
    }

    /// <summary>Entfernt einen Konto-Adapter (F006-T3: Entfernen ohne Neustart).
    /// Gibt false zurück, wenn der Schlüssel nicht registriert war.</summary>
    public bool RemoveAdapter(string key)
    {
        lock (_gate)
        {
            return !string.IsNullOrWhiteSpace(key) && _adapters.Remove(key);
        }
    }

    /// <summary>Alt-Pfad (F001): ruft den einzigen Adapter ab.</summary>
    public async Task<QuotaResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var all = await RefreshAllAsync(cancellationToken);
        return all.Values.Single();
    }

    /// <summary>Ruft jeden Adapter parallel ab (D010: nur bei Öffnen + Refresh, kein Timer),
    /// Schlüssel = Kontoname. Nur die jüngste gestartete Generation einer noch
    /// registrierten Karte darf Cache/Ergebnis aktualisieren; Netzwerk bleibt parallel.</summary>
    public async Task<IReadOnlyDictionary<string, QuotaResult>> RefreshAllAsync(
        CancellationToken cancellationToken = default)
    {
        (string Key, Registration Entry, long Generation)[] registrations;
        lock (_gate)
        {
            registrations = _adapters.Select(p => (p.Key, p.Value, ++p.Value.Generation)).ToArray();
        }

        var fetches = registrations.Select(async pending =>
        {
            QuotaResult result;
            try
            {
                result = await pending.Entry.Adapter.FetchAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = new QuotaResult(false, null, "n/a – Key prüfen / offline");
            }

            return (Pending: pending, Result: result);
        }).ToList();

        var fetched = await Task.WhenAll(fetches);
        lock (_gate)
        {
            foreach (var (pending, result) in fetched)
            {
                if (!_adapters.TryGetValue(pending.Key, out var current)
                    || !ReferenceEquals(current, pending.Entry)
                    || current.Generation != pending.Generation)
                {
                    continue; // Removed, replaced or superseded while awaiting the provider.
                }
                if (result.IsSuccess && result.Snapshot is not null)
                {
                    _cache.Save(result.Snapshot);
                }
                current.Result = result;
            }

            // Even a superseded caller receives the valid current state for rendering.
            return _adapters.Where(p => p.Value.Result is not null)
                .ToDictionary(p => p.Key, p => p.Value.Result!, StringComparer.Ordinal);
        }
    }
}
