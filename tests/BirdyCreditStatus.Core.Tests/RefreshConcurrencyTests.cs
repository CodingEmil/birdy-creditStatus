using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class RefreshConcurrencyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public async Task Removed_account_is_not_restored_by_pending_refresh()
    {
        var cache = new SnapshotCache(_directory);
        var adapter = new ControlledAdapter();
        var service = new RefreshService(new Dictionary<string, IQuotaAdapter> { ["account"] = adapter }, cache);
        var pending = service.RefreshAllAsync();

        Assert.True(service.RemoveAdapter("account"));
        cache.Remove("account");
        adapter.Complete(0, 25);
        var results = await pending;

        Assert.Empty(results);
        Assert.Null(cache.Load("account"));
    }

    [Fact]
    public async Task Older_completion_returns_newer_result_and_preserves_cache()
    {
        var cache = new SnapshotCache(_directory);
        var adapter = new ControlledAdapter();
        var service = new RefreshService(new Dictionary<string, IQuotaAdapter> { ["account"] = adapter }, cache);
        var older = service.RefreshAllAsync();
        var newer = service.RefreshAllAsync();

        adapter.Complete(1, 90);
        var accepted = await newer;
        adapter.Complete(0, 10);
        var late = await older;

        Assert.Equal(90, cache.Load("account")!.Windows.Single().PercentRemaining);
        Assert.Equal(accepted["account"], late["account"]);
    }

    [Fact]
    public async Task Replacing_same_adapter_instance_invalidates_pending_result()
    {
        var cache = new SnapshotCache(_directory);
        var adapter = new ControlledAdapter();
        var service = new RefreshService(new Dictionary<string, IQuotaAdapter> { ["account"] = adapter }, cache);
        var pending = service.RefreshAllAsync();
        service.UpsertAdapter("account", adapter);
        adapter.Complete(0, 10);

        Assert.Empty(await pending);
        Assert.Null(cache.Load("account"));
    }

    private sealed class ControlledAdapter : IQuotaAdapter
    {
        private readonly List<TaskCompletionSource<QuotaResult>> _calls = [];
        public Task<QuotaResult> FetchAsync(CancellationToken cancellationToken = default)
        {
            var pending = new TaskCompletionSource<QuotaResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _calls.Add(pending);
            return pending.Task;
        }
        public void Complete(int index, double remaining) => _calls[index].SetResult(new QuotaResult(
            true, new QuotaSnapshot("account", [new QuotaWindow("5 Stunden", remaining)], DateTimeOffset.UtcNow)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
