using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Kontenliste als JSON in <c>%APPDATA%\birdy-creditStatus\accounts.json</c>
/// (neben <c>snapshot.json</c>, D003). Einträge <c>{ Provider, Name, AuthFilePath }</c>;
/// feldlose Alt-Einträge (F006) gelten als <c>codex</c>, unbekannte Provider fallen raus.
/// Migration: fehlt die Datei, wird jeder vorhandene Default-Login zum Konto
/// (<c>Claude</c>, <c>Codex</c>, <c>OpenCode Go</c> in Blockreihenfolge) — für Go nur
/// bei vorhandenem Key (sonst würde die Harness-Datei jede Maschine befüllen).
/// Fehlend/korrupt/leer = leere Liste, nie Throw (D003-Geist).</summary>
public sealed class AccountStore : IAccountStore
{
    public const string MigratedCodexName = "Codex";
    public const string MigratedClaudeName = "Claude";
    public const string MigratedOpenCodeGoName = "OpenCode Go";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly string _codexDefaultAuthPath;
    private readonly string _claudeDefaultAuthPath;
    private readonly string _openCodeGoDefaultAuthPath;

    public AccountStore(
        string? directoryOverride = null,
        string? codexDefaultAuthPathOverride = null,
        string? claudeDefaultAuthPathOverride = null,
        string? openCodeGoDefaultAuthPathOverride = null)
    {
        var directory = directoryOverride
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "birdy-creditStatus");
        _filePath = Path.Combine(directory, "accounts.json");
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _codexDefaultAuthPath = codexDefaultAuthPathOverride
            ?? Path.Combine(profile, ".codex", "auth.json");
        _claudeDefaultAuthPath = claudeDefaultAuthPathOverride
            ?? Path.Combine(profile, ".claude", ".credentials.json");
        _openCodeGoDefaultAuthPath = openCodeGoDefaultAuthPathOverride
            ?? Path.Combine(profile, ".pi", "agent", "auth.json");
    }

    public IReadOnlyList<Account> Load()
    {
        if (!File.Exists(_filePath))
        {
            return MigrateDefaults();
        }

        string json;
        try
        {
            json = File.ReadAllText(_filePath);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<Account>>(json, JsonOptions);
            if (entries is null)
            {
                return [];
            }

            return entries
                .OfType<Account>() // JSON arrays may contain null even with non-nullable records.
                .Select(e => string.IsNullOrWhiteSpace(e.Provider)
                    ? e with { Provider = AccountProviders.Codex }
                    : e)
                .Where(IsValid)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Save(IReadOnlyList<Account> accounts) => TrySave(accounts);

    private bool TrySave(IReadOnlyList<Account> accounts) =>
        AtomicFile.TryWrite(_filePath, JsonSerializer.Serialize(accounts.Where(IsValid).ToList(), JsonOptions));

    public bool Add(Account account)
    {
        if (!IsValid(account))
        {
            return false;
        }

        var all = Load().ToList();
        if (all.Any(a => a.Name == account.Name))
        {
            return false;
        }

        all.Add(account);
        return TrySave(all);
    }

    public bool Remove(string name)
    {
        var all = Load().ToList();
        return all.RemoveAll(a => a.Name == name) > 0 && TrySave(all);
    }

    public bool Rename(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return false;
        }

        var all = Load().ToList();
        var index = all.FindIndex(a => a.Name == oldName);
        if (index < 0 || all.Any(a => a.Name == newName))
        {
            return false;
        }

        all[index] = all[index] with { Name = newName };
        return TrySave(all);
    }

    private List<Account> MigrateDefaults()
    {
        var migrated = new List<Account>();
        if (File.Exists(_claudeDefaultAuthPath))
        {
            migrated.Add(new Account(AccountProviders.Claude, MigratedClaudeName, _claudeDefaultAuthPath));
        }

        if (File.Exists(_codexDefaultAuthPath))
        {
            migrated.Add(new Account(AccountProviders.Codex, MigratedCodexName, _codexDefaultAuthPath));
        }

        if (HasOpenCodeGoKey(_openCodeGoDefaultAuthPath))
        {
            migrated.Add(new Account(AccountProviders.OpenCodeGo, MigratedOpenCodeGoName, _openCodeGoDefaultAuthPath));
        }

        return migrated;
    }

    private static bool HasOpenCodeGoKey(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("opencode-go", out var section)
                && section.ValueKind == JsonValueKind.Object
                && section.TryGetProperty("key", out var key)
                && key.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(key.GetString());
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsValid(Account account) =>
        AccountProviders.IsKnown(account.Provider)
        && !string.IsNullOrWhiteSpace(account.Name)
        && !string.IsNullOrWhiteSpace(account.AuthFilePath);
}
