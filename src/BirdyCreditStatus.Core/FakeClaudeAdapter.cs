namespace BirdyCreditStatus.Core;

public enum FakeMode
{
    Success,
    NotAvailable,
    LoginRequired,
}

/// <summary>Test-Adapter mit canned Claude-Snapshot (Session, Woche), Rest in Prozent.</summary>
public sealed class FakeClaudeAdapter : IQuotaAdapter
{
    public FakeMode Mode { get; set; } = FakeMode.Success;

    /// <summary>Kontoname als Snapshot-Schlüssel (F007-T2, Default „Claude" für Alt-Tests).</summary>
    public string Provider { get; set; } = "Claude";

    public double SessionPercent { get; set; } = 88;

    public double WeekPercent { get; set; } = 40;

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
            [new QuotaWindow("Session", SessionPercent), new QuotaWindow("Woche", WeekPercent)],
            DateTimeOffset.Now);

        return Task.FromResult(new QuotaResult(true, snapshot));
    }
}
