namespace BirdyCreditStatus.Core;

/// <summary>Gefundener Standard-Login (F010-T1, F011-T1): Anbieter-Kennung, Anzeigename,
/// Pfad und optionale Pi-Section (null = native Datei, gesetzt = Treffer aus einer
/// Pi-Datei, 1 Pfad → N Kandidaten).</summary>
public sealed record DetectedAuth(string Provider, string DisplayName, string Path, string? Section = null);

/// <summary>Erkennung vorhandener Standard-Logins (F010-T1): scannt nur die drei
/// Default-Pfade, Format-Check je Marker via <see cref="AccountValidator"/>, Go nur
/// mit Key, konfigurierte Pfade raus. Nie Throw (D003-Geist).</summary>
public static class DefaultAuthDiscovery
{
    /// <summary>Genau die drei Anbieter-Defaults (Anbieter, Pfad), sonst nichts.</summary>
    public static IReadOnlyList<(string Provider, string Path)> DefaultCandidates() =>
        [.. AccountProviderNames.All.Select(p => (p, AccountProviderPaths.DefaultAuthPath(p)))];

    /// <summary>Scannt die drei Defaults; bereits konfigurierte Pfade fallen raus.</summary>
    public static IReadOnlyList<DetectedAuth> Detect(IEnumerable<string>? configuredPaths = null) =>
        DetectFrom(DefaultCandidates(), configuredPaths);

    /// <summary>Erkennung inkl. Pi-Defaultdatei (F011-T4): native Defaults plus je
    /// formatgültiger Pi-Section ein Treffer (1 Pfad → N Kandidaten via
    /// <c>Section</c>-Feld, geprüft per <see cref="AccountValidator"/>).
    /// Bereits eingerichtete (Pfad, Section)-Paare fallen raus; native Kandidaten auf
    /// der Pi-Datei entfallen, sobald sie Treffer liefert (kein Doppelangebot).
    /// Nie Throw (D003-Geist).</summary>
    public static IReadOnlyList<DetectedAuth> DetectAccounts(IEnumerable<Account> accounts) =>
        DetectAccountsFrom(
            accounts,
            DefaultCandidates(),
            AccountProviderPaths.DefaultAuthPath(AccountProviders.OpenCodeGo));

    /// <summary>Kern (testbar): native Kandidaten plus Pi-Sections aus <c>piPath</c>.</summary>
    public static IReadOnlyList<DetectedAuth> DetectAccountsFrom(
        IEnumerable<Account>? accounts,
        IEnumerable<(string Provider, string Path)> nativeCandidates,
        string? piPath)
    {
        var list = accounts?.ToList() ?? [];
        var configuredPaths = list
            .Where(a => !string.IsNullOrWhiteSpace(a.AuthFilePath))
            .Select(a => a.AuthFilePath)
            .ToList();
        var found = DetectFrom(nativeCandidates, configuredPaths).ToList();
        var piHits = DetectPiHits(piPath, list);
        if (piHits.Count > 0)
        {
            found.RemoveAll(h => string.Equals(h.Path, piPath, StringComparison.OrdinalIgnoreCase));
        }

        found.AddRange(piHits);
        return found;
    }

    private static IReadOnlyList<DetectedAuth> DetectPiHits(string? piPath, IReadOnlyList<Account> accounts)
    {
        if (string.IsNullOrWhiteSpace(piPath))
        {
            return [];
        }

        var hits = new List<DetectedAuth>();
        foreach (var candidate in PiAuthDiscovery.DetectFile(piPath))
        {
            if (accounts.Any(a => string.Equals(a.AuthFilePath, candidate.Path, StringComparison.OrdinalIgnoreCase)
                && string.Equals(EffectiveSection(a), candidate.Section, StringComparison.Ordinal)))
            {
                continue;
            }

            try
            {
                if (AccountValidator.ValidateAuthFile(candidate.Provider, candidate.Path, candidate.Section) is not null)
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                continue;
            }

            hits.Add(candidate);
        }

        return hits;
    }

    /// <summary>Belegte Section eines Kontos: Pi-Section oder — für native Go-Konten,
    /// die stets die <c>opencode-go</c>-Section lesen — diese (Migration!).</summary>
    private static string? EffectiveSection(Account account) =>
        account.AuthSection
        ?? (account.Provider == AccountProviders.OpenCodeGo ? "opencode-go" : null);

    /// <summary>Kern (testbar): prüft beliebige Kandidaten gegen ihr Provider-Format.</summary>
    public static IReadOnlyList<DetectedAuth> DetectFrom(
        IEnumerable<(string Provider, string Path)> candidates,
        IEnumerable<string>? configuredPaths = null)
    {
        var configured = new HashSet<string>(configuredPaths ?? [], StringComparer.OrdinalIgnoreCase);
        var found = new List<DetectedAuth>();
        foreach (var (provider, path) in candidates)
        {
            if (!AccountProviders.IsKnown(provider)
                || string.IsNullOrWhiteSpace(path)
                || configured.Contains(path))
            {
                continue;
            }

            try
            {
                if (!File.Exists(path)
                    || AccountValidator.ValidateAuthFile(provider, path) is not null)
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                continue;
            }

            found.Add(new DetectedAuth(provider, AccountProviderNames.Display(provider), path));
        }

        return found;
    }
}
