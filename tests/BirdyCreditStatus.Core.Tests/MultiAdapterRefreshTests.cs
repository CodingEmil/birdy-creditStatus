using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class MultiAdapterRefreshTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    private static RefreshService TwoFakes(
        string directory, FakeClaudeAdapter claude, FakeCodexAdapter codex) =>
        new(new Dictionary<string, IQuotaAdapter>
        {
            ["Claude"] = claude,
            ["Codex"] = codex,
        }, new SnapshotCache(directory));

    private static RefreshService ThreeFakes(
        string directory, FakeClaudeAdapter claude, FakeCodexAdapter codex, FakeOpenCodeGoAdapter go) =>
        new(new Dictionary<string, IQuotaAdapter>
        {
            ["Claude"] = claude,
            ["Codex"] = codex,
            ["OpenCode Go"] = go,
        }, new SnapshotCache(directory));

    [Fact]
    public async Task Two_fakes_yield_two_cached_cards_with_timestamps()
    {
        var service = TwoFakes(_directory, new FakeClaudeAdapter(), new FakeCodexAdapter());

        var results = await service.RefreshAllAsync();
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.Equal(2, results.Count);
        Assert.True(results["Claude"].IsSuccess);
        Assert.True(results["Codex"].IsSuccess);
        Assert.Equal(["Session", "Woche"], all["Claude"].Windows.Select(w => w.Name));
        Assert.Equal(["5 Stunden", "Woche"], all["Codex"].Windows.Select(w => w.Name));
        Assert.NotEqual(default, all["Claude"].FetchedAt);
        Assert.NotEqual(default, all["Codex"].FetchedAt);
    }

    [Fact]
    public async Task Failure_in_one_card_keeps_other_result_and_cache()
    {
        var cache = new SnapshotCache(_directory);
        var previousCodex = new QuotaSnapshot(
            "Codex",
            [new QuotaWindow("5 Stunden", 11)],
            new DateTimeOffset(2026, 9, 19, 18, 5, 0, TimeSpan.Zero));
        cache.Save(previousCodex);
        var service = TwoFakes(
            _directory,
            new FakeClaudeAdapter { SessionPercent = 25, WeekPercent = 70 },
            new FakeCodexAdapter { Mode = FakeMode.NotAvailable });

        var results = await service.RefreshAllAsync();
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.True(results["Claude"].IsSuccess);
        Assert.False(results["Codex"].IsSuccess);
        Assert.Equal([25.0, 70.0], all["Claude"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal([11.0], all["Codex"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal(previousCodex.FetchedAt, all["Codex"].FetchedAt);
    }

    [Fact]
    public async Task Setup_signal_in_one_card_keeps_other_cache()
    {
        var cache = new SnapshotCache(_directory);
        var previousClaude = new QuotaSnapshot(
            "Claude",
            [new QuotaWindow("Session", 10)],
            new DateTimeOffset(2026, 9, 19, 18, 0, 0, TimeSpan.Zero));
        cache.Save(previousClaude);
        var service = TwoFakes(
            _directory,
            new FakeClaudeAdapter { Mode = FakeMode.LoginRequired },
            new FakeCodexAdapter());

        var results = await service.RefreshAllAsync();
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.True(results["Claude"].LoginRequired);
        Assert.True(results["Codex"].IsSuccess);
        Assert.Equal([10.0], all["Claude"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal(previousClaude.FetchedAt, all["Claude"].FetchedAt);
        Assert.Equal(["5 Stunden", "Woche"], all["Codex"].Windows.Select(w => w.Name));
    }

    [Fact]
    public async Task Throwing_adapter_becomes_na_without_blocking_other_card()
    {
        var service = new RefreshService(
            new Dictionary<string, IQuotaAdapter>
            {
                ["Claude"] = new ThrowingAdapter(),
                ["Codex"] = new FakeCodexAdapter(),
            }, new SnapshotCache(_directory));

        var results = await service.RefreshAllAsync();
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.False(results["Claude"].IsSuccess);
        Assert.True(results["Codex"].IsSuccess);
        Assert.False(all.ContainsKey("Claude"));
        Assert.Equal(["5 Stunden", "Woche"], all["Codex"].Windows.Select(w => w.Name));
    }

    [Fact]
    public async Task Three_fakes_yield_three_cached_cards_with_timestamps()
    {
        var service = ThreeFakes(_directory, new FakeClaudeAdapter(), new FakeCodexAdapter(), new FakeOpenCodeGoAdapter());

        var results = await service.RefreshAllAsync();
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.Equal(3, results.Count);
        Assert.True(results["Claude"].IsSuccess);
        Assert.True(results["Codex"].IsSuccess);
        Assert.True(results["OpenCode Go"].IsSuccess);
        Assert.Equal(["Session", "Woche"], all["Claude"].Windows.Select(w => w.Name));
        Assert.Equal(["5 Stunden", "Woche"], all["Codex"].Windows.Select(w => w.Name));
        Assert.Equal(["Rolling", "Woche", "Monat"], all["OpenCode Go"].Windows.Select(w => w.Name));
        Assert.NotEqual(default, all["Claude"].FetchedAt);
        Assert.NotEqual(default, all["Codex"].FetchedAt);
        Assert.NotEqual(default, all["OpenCode Go"].FetchedAt);
    }

    [Fact]
    public async Task Failure_in_go_card_keeps_other_results_and_cache()
    {
        var cache = new SnapshotCache(_directory);
        var previousGo = new QuotaSnapshot(
            "OpenCode Go",
            [new QuotaWindow("Rolling", 11)],
            new DateTimeOffset(2026, 9, 19, 18, 5, 0, TimeSpan.Zero));
        cache.Save(previousGo);
        var service = ThreeFakes(
            _directory,
            new FakeClaudeAdapter(),
            new FakeCodexAdapter(),
            new FakeOpenCodeGoAdapter { Mode = FakeMode.NotAvailable });

        var results = await service.RefreshAllAsync();
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.True(results["Claude"].IsSuccess);
        Assert.True(results["Codex"].IsSuccess);
        Assert.False(results["OpenCode Go"].IsSuccess);
        Assert.Equal(["Session", "Woche"], all["Claude"].Windows.Select(w => w.Name));
        Assert.Equal(["5 Stunden", "Woche"], all["Codex"].Windows.Select(w => w.Name));
        Assert.Equal([11.0], all["OpenCode Go"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal(previousGo.FetchedAt, all["OpenCode Go"].FetchedAt);
    }

    [Fact]
    public async Task Setup_signal_in_go_card_keeps_other_cache()
    {
        var cache = new SnapshotCache(_directory);
        var previousGo = new QuotaSnapshot(
            "OpenCode Go",
            [new QuotaWindow("Rolling", 10)],
            new DateTimeOffset(2026, 9, 19, 18, 0, 0, TimeSpan.Zero));
        cache.Save(previousGo);
        var service = ThreeFakes(
            _directory,
            new FakeClaudeAdapter(),
            new FakeCodexAdapter(),
            new FakeOpenCodeGoAdapter { Mode = FakeMode.LoginRequired });

        var results = await service.RefreshAllAsync();
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.True(results["Claude"].IsSuccess);
        Assert.True(results["Codex"].IsSuccess);
        Assert.True(results["OpenCode Go"].LoginRequired);
        Assert.Equal([10.0], all["OpenCode Go"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal(previousGo.FetchedAt, all["OpenCode Go"].FetchedAt);
        Assert.Equal(["Session", "Woche"], all["Claude"].Windows.Select(w => w.Name));
        Assert.Equal(["5 Stunden", "Woche"], all["Codex"].Windows.Select(w => w.Name));
    }

    [Fact]
    public async Task Mixed_provider_accounts_stay_isolated_with_account_name_keys()
    {
        var cache = new SnapshotCache(_directory);
        var service = new RefreshService(
            new Dictionary<string, IQuotaAdapter>
            {
                ["Claude privat"] = new FakeClaudeAdapter { Provider = "Claude privat", Mode = FakeMode.NotAvailable },
                ["Codex privat"] = new FakeCodexAdapter { Provider = "Codex privat" },
                ["Go privat"] = new FakeOpenCodeGoAdapter { Provider = "Go privat" },
            },
            cache);

        var results = await service.RefreshAllAsync();
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.Equal(3, results.Count);
        Assert.False(results["Claude privat"].IsSuccess);
        Assert.True(results["Codex privat"].IsSuccess);
        Assert.True(results["Go privat"].IsSuccess);
        Assert.False(all.ContainsKey("Claude privat"));
        Assert.Equal(["5 Stunden", "Woche"], all["Codex privat"].Windows.Select(w => w.Name));
        Assert.Equal(["Rolling", "Woche", "Monat"], all["Go privat"].Windows.Select(w => w.Name));
    }

    private sealed class ThrowingAdapter : IQuotaAdapter
    {
        public Task<QuotaResult> FetchAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
