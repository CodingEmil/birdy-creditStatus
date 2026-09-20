using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class SnapshotCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public void Roundtrip_preserves_snapshot_and_timestamp()
    {
        var cache = new SnapshotCache(_directory);
        var snapshot = new QuotaSnapshot(
            "Claude",
            [new QuotaWindow("Session", 12), new QuotaWindow("Woche", 60)],
            new DateTimeOffset(2026, 9, 19, 18, 0, 0, TimeSpan.Zero));

        cache.Save(snapshot);
        var loaded = cache.Load();

        Assert.NotNull(loaded);
        Assert.Equal("Claude", loaded.Provider);
        Assert.Equal([12.0, 60.0], loaded.Windows.Select(w => w.PercentRemaining));
        Assert.Equal(snapshot.FetchedAt, loaded.FetchedAt);
    }

    [Fact]
    public void Empty_cache_returns_null()
    {
        var cache = new SnapshotCache(_directory);

        Assert.Null(cache.Load());
    }

    [Fact]
    public void Remove_deletes_only_the_named_entry()
    {
        var cache = new SnapshotCache(_directory);
        var now = new DateTimeOffset(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);
        cache.Save(new QuotaSnapshot("Codex privat", [new QuotaWindow("5 Stunden", 72)], now));
        cache.Save(new QuotaSnapshot("Codex Arbeit", [new QuotaWindow("5 Stunden", 11)], now));

        cache.Remove("Codex privat");
        var all = new SnapshotCache(_directory).LoadAll();

        Assert.False(all.ContainsKey("Codex privat"));
        Assert.True(all.ContainsKey("Codex Arbeit"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
