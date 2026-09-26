namespace BirdyCreditStatus.Core;

/// <summary>Verkabelung Konto → Adapter (F007-T2, Pi-Sections seit F011-T3): je Provider
/// der passende Adapter mit Konto-Pfad und Kontoname als Snapshot-Schlüssel.
/// Unbekannter Provider fällt auf Codex zurück, nie Throw (Fabrik selbst wirft nur
/// bei null-Argumenten). Mit gesetzter <c>AuthSection</c> lesen Claude/Codex über
/// read-only Pi-Stores aus der benannten Section (SaveAsync = No-Op, die Pi-Datei
/// wird nie beschrieben), Go über seine Section aus beliebiger Pi-Datei; sonst nativ
/// wie bisher. Rotation (nativ Claude/Codex) schreibt isoliert in die jeweilige
/// Konto-Datei (D012), Go bleibt read-only (D005).</summary>
public static class AccountAdapterFactory
{
    public static IQuotaAdapter Create(Account account, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(http);

        if (!string.IsNullOrWhiteSpace(account.AuthSection))
        {
            return account.Provider switch
            {
                AccountProviders.Claude => new ClaudeQuotaAdapter(
                    http, new PiClaudeCredentialStore(account.AuthFilePath, account.AuthSection),
                    accountName: account.Name),
                AccountProviders.OpenCodeGo => new OpenCodeGoQuotaAdapter(
                    http, StoreOrDefault(account.AuthFilePath, p => new FileOpenCodeGoCredentialStore(p)),
                    accountName: account.Name),
                _ => new CodexQuotaAdapter(
                    http, new PiCodexCredentialStore(account.AuthFilePath, account.AuthSection),
                    accountName: account.Name),
            };
        }

        return account.Provider switch
        {
            AccountProviders.Claude => new ClaudeQuotaAdapter(
                http, StoreOrDefault(account.AuthFilePath, p => new FileClaudeCredentialStore(p)),
                accountName: account.Name),
            AccountProviders.OpenCodeGo => new OpenCodeGoQuotaAdapter(
                http, StoreOrDefault(account.AuthFilePath, p => new FileOpenCodeGoCredentialStore(p)),
                accountName: account.Name),
            _ => new CodexQuotaAdapter(
                http, StoreOrDefault(account.AuthFilePath, p => new FileCodexCredentialStore(p)),
                accountName: account.Name),
        };
    }

    private static T StoreOrDefault<T>(string authFilePath, Func<string?, T> create) =>
        create(string.IsNullOrWhiteSpace(authFilePath) ? null : authFilePath);
}
