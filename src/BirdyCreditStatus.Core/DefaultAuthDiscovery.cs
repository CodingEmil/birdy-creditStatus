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
