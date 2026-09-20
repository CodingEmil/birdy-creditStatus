namespace BirdyCreditStatus.Core;

/// <summary>Test-Adapter mit canned OpenCode-Go-Snapshot (Rolling, Woche, Monat), Rest in Prozent.</summary>
public sealed class FakeOpenCodeGoAdapter : IQuotaAdapter
{
    public FakeMode Mode { get; set; } = FakeMode.Success;

    /// <summary>Kontoname als Snapshot-Schlüssel (F007-T2, Default „OpenCode Go" für Alt-Tests).</summary>
    public string Provider { get; set; } = "OpenCode Go";

    public double RollingPercent { get; set; } = 64;

    public double WeekPercent { get; set; } = 48;

    public double MonthPercent { get; set; } = 81;

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
            [
                new QuotaWindow("Rolling", RollingPercent),
                new QuotaWindow("Woche", WeekPercent),
                new QuotaWindow("Monat", MonthPercent),
            ],
            DateTimeOffset.Now);

        return Task.FromResult(new QuotaResult(true, snapshot));
    }
}
