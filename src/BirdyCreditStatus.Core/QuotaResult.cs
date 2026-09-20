namespace BirdyCreditStatus.Core;

/// <summary>Ergebnis eines Adapter-Abrufs: Snapshot oder n/a-Fehlerzustand.
/// LoginRequired markiert den Setup-Leerzustand (kein Claude-Login vorhanden).</summary>
public sealed record QuotaResult(bool IsSuccess, QuotaSnapshot? Snapshot, string? Error = null, bool LoginRequired = false);
