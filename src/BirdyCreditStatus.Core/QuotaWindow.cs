namespace BirdyCreditStatus.Core;

/// <summary>Ein Kontingent-Fenster (z. B. Session, Woche), Rest in Prozent übrig. ResetsAt null = unbekannt, keine Anzeige.</summary>
public sealed record QuotaWindow(string Name, double PercentRemaining, DateTimeOffset? ResetsAt = null);
