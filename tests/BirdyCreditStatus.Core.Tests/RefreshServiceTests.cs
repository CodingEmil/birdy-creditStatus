using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class RefreshServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public async Task Success_saves_snapshot_to_cache()
    {
        var adapter = new FakeClaudeAdapter { SessionPercent = 25, WeekPercent = 70 };
        var service = new RefreshService(adapter, new SnapshotCache(_directory));

        var result = await service.RefreshAsync();
        var loaded = new SnapshotCache(_directory).Load();

        Assert.True(result.IsSuccess);
        Assert.NotNull(loaded);
        Assert.Equal([25.0, 70.0], loaded.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Failure_keeps_previous_cache()
    {
        var previous = new QuotaSnapshot("Claude", [new QuotaWindow("Session", 10)], DateTimeOffset.UtcNow);
        new SnapshotCache(_directory).Save(previous);
        var service = new RefreshService(new FakeClaudeAdapter { Mode = FakeMode.NotAvailable }, new SnapshotCache(_directory));

        var result = await service.RefreshAsync();

        Assert.False(result.IsSuccess);
        var kept = new SnapshotCache(_directory).Load();
        Assert.NotNull(kept);
        Assert.Equal(previous.Provider, kept.Provider);
        Assert.Equal(previous.Windows.Select(w => (w.Name, w.PercentRemaining)), kept.Windows.Select(w => (w.Name, w.PercentRemaining)));
        Assert.Equal(previous.FetchedAt, kept.FetchedAt);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
