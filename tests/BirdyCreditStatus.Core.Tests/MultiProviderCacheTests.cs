using System.Text.Json;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class MultiProviderCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public void Two_providers_roundtrip_with_timestamps()
    {
        var cache = new SnapshotCache(_directory);
        var claude = new QuotaSnapshot(
            "Claude",
            [new QuotaWindow("Session", 12), new QuotaWindow("Woche", 60)],
            new DateTimeOffset(2026, 9, 19, 18, 0, 0, TimeSpan.Zero));
        var codex = new QuotaSnapshot(
            "Codex",
            [new QuotaWindow("5 Stunden", 72), new QuotaWindow("Woche", 55)],
            new DateTimeOffset(2026, 9, 19, 18, 5, 0, TimeSpan.Zero));

        cache.Save(claude);
        cache.Save(codex);
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.Equal(2, all.Count);
        Assert.Equal([12.0, 60.0], all["Claude"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal(claude.FetchedAt, all["Claude"].FetchedAt);
        Assert.Equal(["5 Stunden", "Woche"], all["Codex"].Windows.Select(w => w.Name));
        Assert.Equal(codex.FetchedAt, all["Codex"].FetchedAt);
    }

    [Fact]
    public void Save_overwrites_only_respective_entry()
    {
        var cache = new SnapshotCache(_directory);
        var codex = new QuotaSnapshot(
            "Codex",
            [new QuotaWindow("5 Stunden", 72)],
            new DateTimeOffset(2026, 9, 19, 18, 5, 0, TimeSpan.Zero));
        cache.Save(new QuotaSnapshot("Claude", [new QuotaWindow("Session", 10)], new DateTimeOffset(2026, 9, 19, 18, 0, 0, TimeSpan.Zero)));
        cache.Save(codex);

        cache.Save(new QuotaSnapshot("Claude", [new QuotaWindow("Session", 99)], new DateTimeOffset(2026, 9, 19, 19, 0, 0, TimeSpan.Zero)));
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.Equal(99, all["Claude"].Windows.Single().PercentRemaining);
        Assert.Equal(codex.FetchedAt, all["Codex"].FetchedAt);
        Assert.Equal([72.0], all["Codex"].Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public void Legacy_single_snapshot_json_migrates_to_provider_entry()
    {
        Directory.CreateDirectory(_directory);
        var legacy = new QuotaSnapshot(
            "Claude",
            [new QuotaWindow("Session", 12), new QuotaWindow("Woche", 60)],
            new DateTimeOffset(2026, 9, 19, 18, 0, 0, TimeSpan.Zero));
        File.WriteAllText(
            Path.Combine(_directory, "snapshot.json"),
            JsonSerializer.Serialize(legacy, new JsonSerializerOptions { WriteIndented = true }));

        var all = new SnapshotCache(_directory).LoadAll();

        Assert.Single(all);
        Assert.Equal([12.0, 60.0], all["Claude"].Windows.Select(w => w.PercentRemaining));
        Assert.Equal(legacy.FetchedAt, all["Claude"].FetchedAt);
    }

    [Fact]
    public void Resets_at_survives_cache_roundtrip()
    {
        var cache = new SnapshotCache(_directory);
        var resetsAt = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        cache.Save(new QuotaSnapshot(
            "Codex",
            [new QuotaWindow("5 Stunden", 72, resetsAt), new QuotaWindow("Woche", 55)],
            DateTimeOffset.UtcNow));

        var all = new SnapshotCache(_directory).LoadAll();

        Assert.Equal(resetsAt, all["Codex"].Windows.First().ResetsAt);
        Assert.Null(all["Codex"].Windows.Last().ResetsAt);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
