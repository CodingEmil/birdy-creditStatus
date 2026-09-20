using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class FakeCodexAdapterTests
{
    [Fact]
    public async Task Returns_five_hour_and_week_windows()
    {
        IQuotaAdapter adapter = new FakeCodexAdapter();

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Codex", result.Snapshot!.Provider);
        Assert.Equal(["5 Stunden", "Woche"], result.Snapshot.Windows.Select(w => w.Name));
    }

    [Fact]
    public async Task Failure_mode_returns_na_result()
    {
        IQuotaAdapter adapter = new FakeCodexAdapter { Mode = FakeMode.NotAvailable };

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.False(result.LoginRequired);
    }

    [Fact]
    public async Task Login_required_mode_returns_setup_state()
    {
        IQuotaAdapter adapter = new FakeCodexAdapter { Mode = FakeMode.LoginRequired };

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.True(result.LoginRequired);
    }
}
