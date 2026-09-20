using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Lokaler JSON-Cache (D003): ein Eintrag je Karte (Schlüssel = Kontoname, F006-T2 —
/// für Einzel-Anbieter identisch zum Provider-Namen), je Eintrag Snapshot plus Abrufzeit.
/// je Eintrag Snapshot plus Abrufzeit. Unkritisch verlierbar, der nächste Poll baut ihn wieder auf.
/// Ein vorhandenes Einzel-snapshot.json (F001-Format) wird beim Laden als Eintrag übernommen.
/// <see cref="Load()"/> ist der Alt-Zugriff für die Einzel-Karten-UI (Claude) und wird in T3 abgelöst.</summary>
public sealed class SnapshotCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public SnapshotCache(string? directoryOverride = null)
    {
        var directory = directoryOverride
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "birdy-creditStatus");
        _filePath = Path.Combine(directory, "snapshot.json");
    }

    /// <summary>Legt den Eintrag für <c>snapshot.Provider</c> ab, andere Einträge bleiben unberührt.</summary>
    public void Save(QuotaSnapshot snapshot)
    {
        var all = LoadAllMutable();
        all[snapshot.Provider] = snapshot;
        Write(all);
    }

    /// <summary>Alt-Zugriff (F001): Claude-Eintrag oder null. Wird in T3 durch kartenweises Laden abgelöst.</summary>
    public QuotaSnapshot? Load() => Load("Claude");

    /// <summary>Lädt den Eintrag eines Providers oder null.</summary>
    public QuotaSnapshot? Load(string provider)
    {
        LoadAllMutable().TryGetValue(provider, out var snapshot);
        return snapshot;
    }

    /// <summary>Migration (F006-T2): übernimmt den Eintrag <c>fromKey</c> auf <c>toKey</c>,
    /// wenn das Ziel fehlt (alter <c>Codex</c>-Eintrag → Kontoname, kein Neuabruf-Zwang,
    /// kein Datenverlust). Quelle bleibt erhalten, bestehendes Ziel wird nie überschrieben.</summary>
    public void Migrate(string fromKey, string toKey)
    {
        if (string.IsNullOrWhiteSpace(fromKey) || string.IsNullOrWhiteSpace(toKey)
            || string.Equals(fromKey, toKey, StringComparison.Ordinal))
        {
            return;
        }

        var all = LoadAllMutable();
        if (!all.TryGetValue(fromKey, out var source) || all.ContainsKey(toKey))
        {
            return;
        }

        all[toKey] = source with { Provider = toKey };
        Write(all);
    }

    /// <summary>Lädt alle Einträge, Schlüssel = Kontoname. Leer bei fehlendem/leerem Cache.</summary>
    public IReadOnlyDictionary<string, QuotaSnapshot> LoadAll() => LoadAllMutable();

    /// <summary>Entfernt den Eintrag <c>key</c> (F006-T3: Konto-Entfernen löscht den
    /// Cache-Eintrag mit, andere Einträge bleiben unberührt). Fehlender Schlüssel
    /// ist kein Fehler.</summary>
    public void Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var all = LoadAllMutable();
        if (all.Remove(key))
        {
            Write(all);
        }
    }

    private Dictionary<string, QuotaSnapshot> LoadAllMutable()
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal);
        }

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("Provider", out var providerElement)
                && providerElement.ValueKind == JsonValueKind.String)
            {
                // F001-Einzelformat → als Eintrag des eigenen Providers übernehmen.
                var single = JsonSerializer.Deserialize<QuotaSnapshot>(json, JsonOptions);
                if (single is null)
                {
                    return new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal);
                }

                return new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal) { [single.Provider] = single };
            }

            return JsonSerializer.Deserialize<Dictionary<string, QuotaSnapshot>>(json, JsonOptions)
                ?? new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // Cache ist unkritisch verlierbar: korrupt → leer, der nächste Poll baut ihn wieder auf.
            return new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal);
        }
    }

    private void Write(Dictionary<string, QuotaSnapshot> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(all, JsonOptions));
    }
}
