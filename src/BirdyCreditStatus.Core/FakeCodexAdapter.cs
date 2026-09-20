namespace BirdyCreditStatus.Core;

/// <summary>Test-Adapter mit canned Codex-Snapshot (5 Stunden, Woche), Rest in Prozent.</summary>
public sealed class FakeCodexAdapter : IQuotaAdapter
{
    public FakeMode Mode { get; set; } = FakeMode.Success;

    /// <summary>Kontoname als Snapshot-Schlüssel (F006-T2, Default „Codex" für Alt-Tests).</summary>
    public string Provider { get; set; } = "Codex";

    public double FiveHourPercent { get; set; } = 72;

    public double WeekPercent { get; set; } = 55;

    public Task<QuotaResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (Mode == FakeMode.NotAvailable)
        {
            return Task.FromResult(new QuotaResult(false, null, "n/a – Key prüfen / offline"));
        }

        if (Mode == FakeMode.LoginRequired)
        {
            return Task.FromResult(new QuotaResult(false, null, "n/a – Key prüfen / offline", LoginRequired: true));
        }

        var snapshot = new QuotaSnapshot(
            Provider,
            [new QuotaWindow("5 Stunden", FiveHourPercent), new QuotaWindow("Woche", WeekPercent)],
            DateTimeOffset.Now);

        return Task.FromResult(new QuotaResult(true, snapshot));
    }
}
