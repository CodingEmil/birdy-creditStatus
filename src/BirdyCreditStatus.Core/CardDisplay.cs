namespace BirdyCreditStatus.Core;

/// <summary>Eine Karten-Zeile: Fenstername, Rest-Text, Balkenwert, relativer Reset ("" = keiner).</summary>
public sealed record CardRow(string WindowName, string ValueText, double BarValue, string ResetText);

/// <summary>Darstellungsmodell einer Anbieter-Karte: Zeilen, Zeitstempel (null = kein Stand),
/// Fehlertext (null = keiner), Setup-Leere. Reines Mapping, das Code-Behind wendet nur an (F002-T3).</summary>
public sealed record CardDisplay(
    string Provider,
    IReadOnlyList<CardRow> Rows,
    string? UpdatedText,
    string? ErrorText,
    bool ShowSetup);

/// <summary>Baut je Karte das Darstellungsmodell: Erfolg → frische Zeilen; Fehler/Setup →
/// stale Cache-Zeilen plus Fehler bzw. Setup-Leere; Cache-only (Start) → stale Zeilen ohne Fehler.</summary>
public static class CardDisplayBuilder
{
    public static CardDisplay Build(
        string provider, QuotaResult? live, QuotaSnapshot? cached, DateTimeOffset now)
    {
        if (live?.LoginRequired == true)
        {
            return new CardDisplay(provider, [], null, null, ShowSetup: true);
        }

        var snapshot = live?.IsSuccess == true ? live.Snapshot : cached;
        if (snapshot is null)
        {
            var error = live is null ? null : live.Error ?? "n/a – Key prüfen / offline";
            return new CardDisplay(provider, [], null, error, ShowSetup: false);
        }

        var rows = snapshot.Windows
            .Select(w => new CardRow(
                w.Name,
                $"{w.PercentRemaining:0} % übrig",
                w.PercentRemaining,
                RelativeTime.Format(w.ResetsAt, now)))
            .ToList();

        var errorText = live?.IsSuccess == false
            ? live.Error ?? "n/a – Key prüfen / offline"
            : null;

        return new CardDisplay(
            provider,
            rows,
            $"Zuletzt aktualisiert um {snapshot.FetchedAt.LocalDateTime:HH:mm}",
            errorText,
            ShowSetup: false);
    }
}
