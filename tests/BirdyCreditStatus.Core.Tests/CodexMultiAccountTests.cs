using System.Net;
using System.Text;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht B (F006-T2): Kontonamen als Schlüssel — ein Adapter je Konto.</summary>
public sealed class CodexMultiAccountTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    private const string UsageJson =
        """{"rate_limit":{"primary_window":{"used_percent":10},"secondary_window":{"used_percent":20}}}""";

    [Fact]
    public async Task Codex_adapter_with_account_name_uses_it_as_provider()
    {
        var adapter = new CodexQuotaAdapter(
            StubHttp(UsageJson),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")),
            accountName: "Codex privat");

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Codex privat", result.Snapshot!.Provider);
    }

    [Fact]
    public async Task Two_codex_accounts_success_and_failure_stay_isolated()
    {
        var cache = new SnapshotCache(_directory);
        var previousFailing = new QuotaSnapshot(
            "Codex Arbeit",
            [new QuotaWindow("5 Stunden", 11)],
            new DateTimeOffset(2026, 9, 19, 18, 5, 0, TimeSpan.Zero));
        cache.Save(previousFailing);
        var service = new RefreshService(
            new Dictionary<string, IQuotaAdapter>
            {
                ["Codex privat"] = new FakeCodexAdapter { Provider = "Codex privat", FiveHourPercent = 90 },
                ["Codex Arbeit"] = new FakeCodexAdapter { Provider = "Codex Arbeit", Mode = FakeMode.NotAvailable },
            },
            cache);

        var results = await service.RefreshAllAsync();
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.Equal(2, results.Count);
        Assert.True(results["Codex privat"].IsSuccess);
        Assert.False(results["Codex Arbeit"].IsSuccess);
        Assert.Equal("n/a – Key prüfen / offline", results["Codex Arbeit"].Error);
        Assert.Equal([90.0, 55.0], all["Codex privat"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal([11.0], all["Codex Arbeit"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal(previousFailing.FetchedAt, all["Codex Arbeit"].FetchedAt);
    }

    [Fact]
    public void Legacy_codex_cache_migrates_to_account_name_without_refetch()
    {
        var cache = new SnapshotCache(_directory);
        var legacy = new QuotaSnapshot(
            "Codex",
            [new QuotaWindow("5 Stunden", 72), new QuotaWindow("Woche", 55)],
            new DateTimeOffset(2026, 9, 19, 18, 5, 0, TimeSpan.Zero));
        cache.Save(legacy);

        cache.Migrate("Codex", "Codex privat");
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.True(all.ContainsKey("Codex privat"));
        Assert.Equal([72.0, 55.0], all["Codex privat"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal(legacy.FetchedAt, all["Codex privat"].FetchedAt);
        Assert.Equal("Codex privat", all["Codex privat"].Provider);
        Assert.True(all.ContainsKey("Codex"));
    }

    [Fact]
    public async Task Upsert_and_remove_adapter_change_refresh_without_restart()
    {
        var service = new RefreshService(
            new Dictionary<string, IQuotaAdapter>
            {
                ["Claude"] = new FakeClaudeAdapter(),
            },
            new SnapshotCache(_directory));

        service.UpsertAdapter("Codex privat", new FakeCodexAdapter { Provider = "Codex privat" });
        var added = await service.RefreshAllAsync();

        Assert.True(added.ContainsKey("Codex privat"));
        Assert.True(added["Codex privat"].IsSuccess);

        Assert.True(service.RemoveAdapter("Codex privat"));
        Assert.False(service.RemoveAdapter("Codex privat"));
        var removed = await service.RefreshAllAsync();

        Assert.False(removed.ContainsKey("Codex privat"));
        Assert.True(removed.ContainsKey("Claude"));
    }

    [Fact]
    public void Account_card_keeps_windows_and_uebrig_vocabulary()
    {
        var now = new DateTimeOffset(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);
        var snapshot = new QuotaSnapshot(
            "Codex privat",
            [new QuotaWindow("5 Stunden", 72), new QuotaWindow("Woche", 55)],
            now);

        var display = CardDisplayBuilder.Build("Codex privat", new QuotaResult(true, snapshot), null, now);

        Assert.Equal("Codex privat", display.Provider);
        Assert.Equal(["5 Stunden", "Woche"], display.Rows.Select(r => r.WindowName));
        Assert.Equal(["72 % übrig", "55 % übrig"], display.Rows.Select(r => r.ValueText));
    }

    private static HttpClient StubHttp(string json) =>
        new(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class StubStore(CodexOAuthCredentials? credentials) : ICodexCredentialStore
    {
        public Task<CodexOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(credentials);

        public Task SaveAsync(CodexOAuthCredentials credentials, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
